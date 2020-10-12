using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Util;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using Torch.Utils.Reflected;
using VRage.Collections;
using VRage.Game.Entity;

namespace Profiler.Core
{
    [ReflectedLazy]
    internal static class ProfilerPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        #region Patch Targets

        // ReSharper disable InconsistentNaming
#pragma warning disable 649
        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateBeforeSimulation))]
        private static readonly MethodInfo _gameLogicUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateAfterSimulation))]
        private static readonly MethodInfo _gameLogicUpdateAfterSimulation;

        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateOnceBeforeFrame))]
        private static readonly MethodInfo _gameLogicUpdateOnceBeforeFrame;

        [ReflectedMethodInfo(typeof(Sandbox.Engine.Platform.Game), nameof(Sandbox.Engine.Platform.Game.RunSingleFrame))]
        private static readonly MethodInfo _gameRunSingleFrame;

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), "RunSandboxedProgramAction")]
        private static readonly MethodInfo _programmableRunSandboxed;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming

        #endregion

        private static readonly ListReader<string> ParallelEntityUpdateMethods = new List<string>
        {
            "UpdateBeforeSimulation",
            "UpdateBeforeSimulation10",
            "UpdateBeforeSimulation100",
            "ParallelUpdateHandlerAfterSimulation",
            "UpdateAfterSimulation",
            "UpdateAfterSimulation10",
            "UpdateAfterSimulation100",
            "DispatchOnceBeforeFrame",
            "DispatchSimulate",
        };

        private static readonly MethodInfo _generalizedUpdateTranspiler = ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(TranspilerForUpdate));

        public static void Patch(PatchContext ctx)
        {
            ReflectedManager.Process(typeof(ProfilerPatch));

            ctx.GetPattern(_gameRunSingleFrame).Suffixes.Add(ProfilerData.DoTick);

            foreach (var parallelUpdateMethod in ParallelEntityUpdateMethods)
            {
                var method = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(parallelUpdateMethod, ReflectionUtils.StaticFlags | ReflectionUtils.InstanceFlags);
                if (method != null)
                {
                    ctx.GetPattern(method).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }
                else
                {
                    Log.Error($"Unable to find {typeof(MyParallelEntityUpdateOrchestrator)}#{parallelUpdateMethod}.  Some profiling data will be missing");
                }
            }

            ctx.GetPattern(_gameLogicUpdateOnceBeforeFrame).PostTranspilers.Add(_generalizedUpdateTranspiler);

            if (MyDistributedUpdaterReflection.ApiExists())
            {
                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(ctx, _gameLogicUpdateBeforeSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }

                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(ctx, _gameLogicUpdateAfterSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }
            }
            else
            {
                Log.Error("Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Some profiling data will be missing.");
            }

            ctx.GetPattern(_programmableRunSandboxed).Prefixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(PrefixProfilePb)));
            ctx.GetPattern(_programmableRunSandboxed).Suffixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(SuffixProfilePb)));
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable once SuggestBaseTypeForParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrefixProfilePb(MyProgrammableBlock __instance, ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = ProfilerData.StartProgrammableBlock(__instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SuffixProfilePb(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerData.StopToken(__localProfilerHandle, true);
        }

        private static IEnumerable<MsilInstruction> TranspilerForUpdate(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = $"{__methodBase.DeclaringType?.FullName}#{__methodBase.Name}";
            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var il = instructions.ToList();

            var foundAny = false;
            for (var idx = 0; idx < il.Count; idx++)
            {
                var insn = il[idx];
                if ((insn.OpCode == OpCodes.Call || insn.OpCode == OpCodes.Callvirt) && insn.Operand is MsilOperandInline<MethodBase> methodOperand)
                {
                    var method = methodOperand.Value;
                    if (method.Name.StartsWith("UpdateBeforeSimulation")
                        || method.Name.StartsWith("UpdateAfterSimulation")
                        || method.Name == "UpdateOnceBeforeFrame"
                        || method.Name == "Simulate")
                    {
                        var methodName = $"{method.DeclaringType?.FullName}#{method.Name}";

                        if (method.IsStatic)
                        {
                            Log.Error($"Failed attaching profiling to {methodName} in {methodBaseName}.  It's static");
                            continue;
                        }

                        // Valid to inject before this point
                        var methodCallPoint = idx;
                        var validInjectionPoint = methodCallPoint;
                        var additionalStackEntries = method.GetParameters().Length;
                        while (additionalStackEntries > 0) additionalStackEntries -= il[--validInjectionPoint].StackChange();

                        if (additionalStackEntries < 0)
                        {
                            Log.Error(
                                $"Failed attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}."
                                + $"  Running back through the parameters left the stack in an invalid state.");
                            continue;
                        }

                        foundAny = true;
                        Log.Debug($"Attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}");
                        var startProfiler = new[]
                        {
                            new MsilInstruction(OpCodes.Dup), // duplicate the object the update is called on
                            // Grab a profiling token
                            new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.GetGenericProfilerToken),
                            profilerEntry.AsValueStore(),
                        };

                        il.InsertRange(validInjectionPoint, startProfiler);
                        methodCallPoint += startProfiler.Length;

                        var stopProfiler = new[]
                        {
                            // Stop the profiler
                            profilerEntry.AsReferenceLoad(),
                            new MsilInstruction(method.Name.EndsWith("Parallel") ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1), // isMainThread
                            new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.StopProfilerToken),
                        };

                        il.InsertRange(methodCallPoint + 1, stopProfiler);
                        idx = methodCallPoint + stopProfiler.Length - 1;
                    }
                }
            }

            if (!foundAny)
            {
                Log.Error($"Didn't find any update profiling targets for {methodBaseName}.  Some profiling data will be missing");
            }

            return il;
        }
    }
}