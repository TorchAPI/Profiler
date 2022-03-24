using System.Collections.Generic;
using Havok;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class PhysicsSimulateMtProfiler : BaseProfiler<HkWorld>
    {
        protected override void Accept(in ProfilerResult profilerResult, ICollection<HkWorld> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.PhysicsSimulate) return;
            if (profilerResult.GameEntity is not HkWorld world) return;
            acceptedKeys.Add(world);
        }
    }
}