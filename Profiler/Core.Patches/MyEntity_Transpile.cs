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
    // for MyEntity and MyEntityComponentBase
    public static class MyEntity_Transpile
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MyEntity_Transpile);
        static readonly MethodInfo StartTokenFunc = SelfType.GetStaticMethod(nameof(StartToken));

        public static void Patch(PatchContext ctx, MethodBase method)
        {
            var transpiler = SelfType.GetStaticMethod(nameof(Transpile));
            ctx.GetPattern(method).PostTranspilers.Add(transpiler);
        }

        static IEnumerable<MsilInstruction> Transpile(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = $"{__methodBase.DeclaringType?.FullName}#{__methodBase.Name}";
            Log.Trace($"Starting Transpile for method {methodBaseName}");

            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var il = instructions.ToList();

            var foundAny = false;
            for (var idx = 0; idx < il.Count; idx++)
            {
                var insn = il[idx];
                if (insn.OpCode != OpCodes.Call && insn.OpCode != OpCodes.Callvirt) continue;
                if (!(insn.Operand is MsilOperandInline<MethodBase> methodOperand)) continue;

                var method = methodOperand.Value;
                Log.Trace($"Found method {method.Name}");

                if (!method.Name.StartsWith("UpdateBeforeSimulation") &&
                    !method.Name.StartsWith("UpdateAfterSimulation") &&
                    method.Name != "UpdateOnceBeforeFrame" &&
                    method.Name != "Simulate")
                    continue;

                var methodName = $"{method.DeclaringType?.FullName}#{method.Name}";
                Log.Trace($"Matched method name {methodName}");

                if (method.IsStatic)
                {
                    Log.Error($"Failed attaching profiling to {methodName} in {methodBaseName}.  It's static");
                    continue;
                }

                // Valid to inject before this point
                var methodCallPoint = idx;
                var validInjectionPoint = methodCallPoint;
                var additionalStackEntries = method.GetParameters().Length;
                while (additionalStackEntries > 0)
                {
                    additionalStackEntries -= il[--validInjectionPoint].StackChange();
                }

                if (additionalStackEntries < 0)
                {
                    Log.Error(
                        $"Failed attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}."
                        + "  Running back through the parameters left the stack in an invalid state.");
                    continue;
                }

                foundAny = true;

                var methodIndex = StringIndexer.Instance.IndexOf(methodName);

                Log.Trace($"Attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}");
                var startProfiler = new[]
                {
                    new MsilInstruction(OpCodes.Dup), // duplicate the object the update is called on
                    new MsilInstruction(OpCodes.Ldc_I4).InlineValue(methodIndex), // pass the method name
                    // Grab a profiling token
                    new MsilInstruction(OpCodes.Call).InlineValue(StartTokenFunc),
                    profilerEntry.AsValueStore(),
                };

                il.InsertRange(validInjectionPoint, startProfiler);
                methodCallPoint += startProfiler.Length;

                var stopProfiler = new[]
                {
                    // Stop the profiler
                    profilerEntry.AsReferenceLoad(),
                    new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc),
                };

                il.InsertRange(methodCallPoint + 1, stopProfiler);
                idx = methodCallPoint + stopProfiler.Length - 1;
            }

            if (!foundAny)
            {
                Log.Error($"Didn't find any update profiling targets for {methodBaseName}.  Some profiling data will be missing");
            }

            Log.Trace($"Finished Transpile for method {methodBaseName}");

            return il;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartToken(object obj, int methodIndex)
        {
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.General);
        }
    }
}