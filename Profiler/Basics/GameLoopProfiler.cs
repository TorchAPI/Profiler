using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class GameLoopProfiler : BaseProfiler<ProfilerCategory>
    {
        protected override bool TryAccept(in ProfilerResult profilerResult, out ProfilerCategory key)
        {
            key = profilerResult.Category;
            switch (profilerResult.Category)
            {
                case ProfilerCategory.Update:
                case ProfilerCategory.UpdateNetwork:
                case ProfilerCategory.UpdateReplication:
                case ProfilerCategory.UpdateSessionComponents:
                case ProfilerCategory.UpdateSessionComponentsAll:
                case ProfilerCategory.UpdateGps:
                case ProfilerCategory.UpdateParallelWait:
                case ProfilerCategory.Lock:
                case ProfilerCategory.Frame:
                    return true;
                default: return false;
            }
        }
    }
}