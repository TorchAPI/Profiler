using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Core.Patches;
using Profiler.TorchUtils;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using Torch.Utils.Reflected;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;

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

        static readonly MethodInfo GetGenericProfilerToken = typeof(ProfilerPatch).StaticMethod(nameof(Start));

        public static readonly MethodInfo StopProfilerToken = typeof(ProfilerPatch).StaticMethod(nameof(StopToken));

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

#pragma warning restore 649
        // ReSharper restore InconsistentNaming

        #endregion

        private static readonly MethodInfo _generalizedUpdateTranspiler = typeof(ProfilerPatch).StaticMethod(nameof(TranspilerForUpdate));

        public static bool Enabled { get; set; } = true;

        internal static void Patch(PatchContext ctx)
        {
            Log.Trace("Profiler patch started");

            ReflectedManager.Process(typeof(ProfilerPatch));

            foreach (var parallelUpdateMethod in ParallelEntityUpdateMethods)
            {
                var method = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(parallelUpdateMethod, ReflectionUtils.StaticFlags | ReflectionUtils.InstanceFlags);
                if (method == null)
                {
                    Log.Error($"Unable to find {typeof(MyParallelEntityUpdateOrchestrator)}#{parallelUpdateMethod}.  Some profiling data will be missing");
                    continue;
                }

                ctx.GetPattern(method).PostTranspilers.Add(_generalizedUpdateTranspiler);
            }

            ctx.GetPattern(_gameLogicUpdateOnceBeforeFrame).PostTranspilers.Add(_generalizedUpdateTranspiler);

            if (MyDistributedUpdaterReflection.ApiExists())
            {
                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(_gameLogicUpdateBeforeSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }

                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(_gameLogicUpdateAfterSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }
            }
            else
            {
                Log.Error("Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Some profiling data will be missing.");
            }

            MyProgrammableBlock_RunSandboxedProgramAction.Patch(ctx);

            Game_UpdateInternal.Patch(ctx);
            {
                MyTransportLayer_Tick.Patch(ctx);
                MyGameService_Update.Patch(ctx);
                MyNetworkReader_Process.Patch(ctx);
                MyDedicatedServer_ReportReplicatedObjects.Patch(ctx);
                {
                    MySession_Update_Transpile.Patch(ctx);
                    MyReplicationServer_UpdateBefore.Patch(ctx);
                    MySession_UpdateComponents.Patch(ctx);
                    {
                        MySession_UpdateComponents_Transpile.Patch(ctx);
                    }
                    MyGpsCollection_Update.Patch(ctx);
                    MyReplicationServer_UpdateAfter.Patch(ctx);
                    MyDedicatedServer_Tick.Patch(ctx);
                    MyPlayerCollection_SendDirtyBlockLimits.Patch(ctx);
                }
            }

            Log.Trace("Profiler patch ended");
        }

        private static IEnumerable<MsilInstruction> TranspilerForUpdate(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = $"{__methodBase.DeclaringType?.FullName}#{__methodBase.Name}";
            Log.Trace($"Starting TranspilerForUpdate for method {methodBaseName}");

            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var il = instructions.ToList();

            var foundAny = false;
            for (var idx = 0; idx < il.Count; idx++)
            {
                var insn = il[idx];
                if ((insn.OpCode == OpCodes.Call || insn.OpCode == OpCodes.Callvirt) && insn.Operand is MsilOperandInline<MethodBase> methodOperand)
                {
                    var method = methodOperand.Value;
                    Log.Trace($"Found method {method.Name} (may not patch)");

                    if (method.Name.StartsWith("UpdateBeforeSimulation")
                        || method.Name.StartsWith("UpdateAfterSimulation")
                        || method.Name == "UpdateOnceBeforeFrame"
                        || method.Name == "Simulate")
                    {
                        var methodName = $"{method.DeclaringType?.FullName}#{method.Name}";
                        Log.Trace($"Matched method name {methodName} (may not patch)");

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

                        var mappingIndex = StringIndexer.Instance.IndexOf(methodName);

                        Log.Trace($"Attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}");
                        var startProfiler = new[]
                        {
                            new MsilInstruction(OpCodes.Dup), // duplicate the object the update is called on
                            new MsilInstruction(OpCodes.Ldc_I4).InlineValue(mappingIndex), // pass the method name
                            // Grab a profiling token
                            new MsilInstruction(OpCodes.Call).InlineValue(GetGenericProfilerToken),
                            profilerEntry.AsValueStore(),
                        };

                        il.InsertRange(validInjectionPoint, startProfiler);
                        methodCallPoint += startProfiler.Length;

                        var stopProfiler = new[]
                        {
                            // Stop the profiler
                            profilerEntry.AsReferenceLoad(),
                            new MsilInstruction(method.Name.EndsWith("Parallel") ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1), // isMainThread
                            new MsilInstruction(OpCodes.Call).InlineValue(StopProfilerToken),
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

            Log.Trace($"Finished TranspilerForUpdate for method {methodBaseName}");

            return il;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? Start(object obj, int mappingIndex)
        {
            switch (obj)
            {
                case MyEntityComponentBase componentBase:
                {
                    return new ProfilerToken(componentBase.Entity, mappingIndex, ProfilerCategory.General);
                }
                case IMyEntity entity:
                {
                    return new ProfilerToken(entity, mappingIndex, ProfilerCategory.General);
                }
                default:
                {
                    return new ProfilerToken(null, mappingIndex, ProfilerCategory.General);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StopToken(in ProfilerToken? tokenOrNull, bool mainThreadUpdate)
        {
            if (!Enabled) return;

            if (!(tokenOrNull is ProfilerToken token)) return;

            var result = new ProfilerResult(token, mainThreadUpdate);

            ProfilerResultQueue.Instance.Enqueue(result);
        }
    }
}