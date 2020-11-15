using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class GameLoopProfiler : BaseProfiler<string>
    {
        protected override bool TryAccept(ProfilerResult profilerResult, out string key)
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
                case ProfilerCategory.UpdateParallelWait: return true;
                default: return false;
            }
        }
    }
}