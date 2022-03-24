using System.Collections.Generic;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class PhysicsSimulateProfiler : BaseProfiler<string>
    {
        protected override void Accept(in ProfilerResult profilerResult, ICollection<string> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.PhysicsSimulate) return;
            acceptedKeys.Add(profilerResult.MethodName);
        }
    }
}