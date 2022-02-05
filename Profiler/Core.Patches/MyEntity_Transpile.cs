using System;
using System.Collections.Generic;
using System.Linq;
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
            method = default;

            if (insn.OpCode != OpCodes.Call && insn.OpCode != OpCodes.Callvirt) return false;
            if (insn.Operand is not MsilOperandInline<MethodBase> methodOperand) return false;

            method = methodOperand.Value;
            return IsUpdateMethod(method.Name);
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
            var patchedInsns = TranspileImpl(instructions.ToArray(), __localCreator, __methodBase).ToArray();

            Log.Trace("PATCHED IL:");
            foreach (var insn in patchedInsns)
            {
                Log.Trace(insn);
            }

            Log.Trace("PATCHED IL DONE");

            return patchedInsns;
        }

        static IEnumerable<MsilInstruction> TranspileImpl(
            IReadOnlyList<MsilInstruction> insns,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var methodBaseName = NameMethod(__methodBase);
            Log.Trace($"Starting Transpile for method {methodBaseName}");

            var foundAny = false;
            for (var i = 0; i < insns.Count; i++)
            {
                var insn = insns[i];
                Log.Trace(insn);

                if (TryGetUpdateMethod(insn, out var method))
                {
                    Log.Trace($"Found method {method.Name}, static: {method.IsStatic}, instruction: {insn}, base method: {methodBaseName}");

                    var methodName = NameMethod(method);
                    var methodIndex = StringIndexer.Instance.IndexOf(methodName);

                    foundAny = true;

                    // start profiling

                    var callerIndexOffset = method.GetParameters().Length + (method.IsStatic ? 0 : 1); // static if patched by other plugins
                    var callerInsn = insns[i - callerIndexOffset];
                    yield return callerInsn; // pass the caller instance as StartToken() 1st arg
                    Log.Trace($"caller insn: {callerInsn}");

                    yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(methodIndex); // pass the method name as StartToken() 2nd arg
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