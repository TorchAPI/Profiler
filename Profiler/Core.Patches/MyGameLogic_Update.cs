using System;
using NLog;
using Profiler.Utils;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;

namespace Profiler.Core.Patches
{
    public static class MyGameLogic_Update
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type Type = typeof(MyGameLogic);

        public static void Patch(PatchContext ctx)
        {
            var UpdateOnceBeforeFrameMethod = Type.GetStaticMethod(nameof(MyGameLogic.UpdateOnceBeforeFrame));
            MyEntity_Transpile.Patch(ctx, UpdateOnceBeforeFrameMethod);

            if (MyDistributedUpdater_Iterate.ApiExists())
            {
                var UpdateBeforeSimulationMethod = Type.GetStaticMethod(nameof(MyGameLogic.UpdateBeforeSimulation));
                foreach (var updateMethod in MyDistributedUpdater_Iterate.FindUpdateMethods(UpdateBeforeSimulationMethod))
                {
                    MyEntity_Transpile.Patch(ctx, updateMethod);
                }

                var UpdateAfterSimulationMethod = Type.GetStaticMethod(nameof(MyGameLogic.UpdateAfterSimulation));
                foreach (var updateMethod in MyDistributedUpdater_Iterate.FindUpdateMethods(UpdateAfterSimulationMethod))
                {
                    MyEntity_Transpile.Patch(ctx, updateMethod);
                }
            }
            else
            {
                Log.Error("Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Some profiling data will be missing.");
            }
        }
    }
}