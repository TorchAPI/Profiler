using Profiler.Core;
using VRage.Game.Components;

namespace Profiler.Basics
{
    public sealed class SessionComponentsProfiler : BaseProfiler<MySessionComponentBase>
    {
        protected override bool TryAccept(in ProfilerResult profilerResult, out MySessionComponentBase key)
        {
            key = default;
            if (profilerResult.Category != ProfilerCategory.UpdateSessionComponents) return false;

            key = profilerResult.GameEntity as MySessionComponentBase;
            return true;
        }
    }
}