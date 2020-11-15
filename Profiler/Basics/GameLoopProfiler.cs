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
                case ProfilerCategory.Update: return true;
                case ProfilerCategory.UpdateNetwork: return true;
                case ProfilerCategory.UpdateReplication: return true;
                case ProfilerCategory.UpdateSessionComponents: return true;
                default: return false;
            }
        }
    }
}