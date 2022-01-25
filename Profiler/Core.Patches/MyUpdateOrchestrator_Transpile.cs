using System;
using System.Collections.Generic;
using NLog;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Collections;

namespace Profiler.Core.Patches
{
    public static class MyUpdateOrchestrator_Transpile
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly Type Type = typeof(MyUpdateOrchestrator);

        static readonly ListReader<string> Methods = new List<string>
        {
            "DispatchOnceBeforeFrame",
            "DispatchBeforeSimulation",
            "DispatchSimulate",
            "DispatchAfterSimulation",
        };

        public static void Patch(PatchContext ctx)
        {
            try
            {
                foreach (var parallelUpdateMethod in Methods)
                {
                    var method = Type.GetMethod(parallelUpdateMethod, ReflectionUtils.StaticFlags | ReflectionUtils.InstanceFlags);
                    MyEntity_Transpile.Patch(ctx, method);
                    foreach (var updateMethod in MyDistributedUpdater_Iterate.FindUpdateMethods(method))
                    {
                        MyEntity_Transpile.Patch(ctx, updateMethod);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to patch: {e.Message}");
            }
        }
    }
}