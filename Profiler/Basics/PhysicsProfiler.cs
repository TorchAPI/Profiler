using System.Collections.Generic;
using Havok;
using Profiler.Core;
using Profiler.Core.Patches;

namespace Profiler.Basics
{
    public sealed class PhysicsProfiler : BaseProfiler<HkWorld>
    {
        readonly object _flag = new();

        public override void MarkStart()
        {
            base.MarkStart();

            MyPhysics_StepWorlds.FlagContinuous(_flag);
        }

        public override void MarkEnd()
        {
            base.MarkEnd();
            MyPhysics_StepWorlds.UnflagContinuous(_flag);
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<HkWorld> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.Physics) return;
            if (profilerResult.GameEntity is not HkWorld world) return;
            acceptedKeys.Add(world);
        }

        public override void Dispose()
        {
            MarkEnd();
            base.Dispose();
        }
    }
}