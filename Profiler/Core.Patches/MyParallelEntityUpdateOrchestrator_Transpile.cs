using System;
using System.Collections.Generic;
using NLog;
using Utils.General;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Collections;

namespace Profiler.Core.Patches
{
    public static class MyParallelEntityUpdateOrchestrator_Transpile
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly Type Type = typeof(MyParallelEntityUpdateOrchestrator);

        static readonly ListReader<string> Methods = new List<string>
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

        public static void Patch(PatchContext ctx)
        {
            foreach (var parallelUpdateMethod in Methods)
            {
                var method = Type.GetMethod(parallelUpdateMethod, ReflectionUtils.StaticFlags | ReflectionUtils.InstanceFlags);
                if (method == null)
                {
                    Log.Error($"Unable to find {Type}#{parallelUpdateMethod}.  Some profiling data will be missing");
                    continue;
                }

                MyEntity_Transpile.Patch(ctx, method);
            }
        }
    }
}