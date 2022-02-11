using System.Collections.Generic;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class GameLoopProfiler : BaseProfiler<ProfilerCategory>
    {
        protected override void Accept(in ProfilerResult profilerResult, ICollection<ProfilerCategory> acceptedKeys)
        {
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
                {
                    acceptedKeys.Add(profilerResult.Category);
                    return;
                }
                default:
                {
                    return;
                }
            }
        }
    }
}