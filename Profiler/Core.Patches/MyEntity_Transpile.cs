using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core.Patches
{
    public static class MyEntity_Transpile
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MyEntity_Transpile);
        static readonly MethodInfo StartTokenFunc = SelfType.GetStaticMethod(nameof(StartToken));
        static readonly MethodInfo Transpiler = SelfType.GetStaticMethod(nameof(Transpile));

        // profile all Update methods found inside given method
        public static void Patch(PatchContext ctx, MethodBase method)
        {
            ctx.GetPattern(method).PostTranspilers.Add(Transpiler);
        }

        static bool IsUpdateMethod(string methodName)
        {
            return methodName.StartsWith("UpdateBeforeSimulation") ||
                   methodName.StartsWith("UpdateAfterSimulation") ||
                   methodName is "UpdateOnceBeforeFrame" or "Simulate";
        }

        //todo move to utils
        public static bool TryGetUpdateMethod(MsilInstruction insn, out MethodBase method)
        {
            if ((insn.OpCode == OpCodes.Call || insn.OpCode == OpCodes.Callvirt) &&
                insn.Operand is MsilOperandInline<MethodBase> methodOperand)
            {
                method = methodOperand.Value;
                Log.Trace($"Found method {method.Name}");

                if (IsUpdateMethod(method.Name))
                {
                    return true;
                }
            }

            method = default;
            return false;
        }

        //todo move to utils
        public static string NameMethod(MethodBase method)
        {
            return $"{method.DeclaringType?.FullName}#{method.Name}";
        }

        static IEnumerable<MsilInstruction> Transpile(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = NameMethod(__methodBase);
            Log.Trace($"Starting Transpile for method {methodBaseName}");

            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var foundAny = false;
            foreach (var insn in instructions)
            {
                if (TryGetUpdateMethod(insn, out var method))
                {
                    var methodName = NameMethod(method);
                    var methodIndex = StringIndexer.Instance.IndexOf(methodName);

                    foundAny = true;

                    // start profiling
                    yield return new MsilInstruction(method.IsStatic ? OpCodes.Ldnull : OpCodes.Dup); // method "can" be static if patched by other plugins
                    yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(methodIndex); // pass the method name
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(StartTokenFunc); // Grab a profiling token
                    yield return profilerEntry.AsValueStore();

                    // call the update method
                    yield return insn;

                    // end profiling
                    yield return profilerEntry.AsReferenceLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc);
                }
                else
                {
                    yield return insn;
                }
            }

            if (!foundAny)
            {
                Log.Error($"Didn't find any update profiling targets for {methodBaseName}.  Some profiling data will be missing");
            }

            Log.Trace($"Finished Transpile for method {methodBaseName}");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartToken(object obj, int methodIndex)
        {
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.General);
        }
    }
}