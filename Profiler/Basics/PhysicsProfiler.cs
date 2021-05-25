using Havok;
using Profiler.Core;
using Profiler.Core.Patches;

namespace Profiler.Basics
{
    public sealed class PhysicsProfiler : BaseProfiler<HkWorld>
    {
        public override void MarkStart()
        {
            base.MarkStart();

            MyPhysics_StepWorlds.SimulatesParallel = false;
        }

        public void MarkEnd()
        {
            MyPhysics_StepWorlds.SimulatesParallel = true;
        }

        public override void Dispose()
        {
            MarkEnd();
            base.Dispose();
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out HkWorld key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.Physics) return false;
            if (profilerResult.GameEntity is not HkWorld world) return false;

            key = world;
            return true;
        }
    }
}