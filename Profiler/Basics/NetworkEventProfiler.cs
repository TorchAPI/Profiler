using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class NetworkEventProfiler : BaseProfiler<string>
    {
        protected override bool TryAccept(in ProfilerResult profilerResult, out string key)
        {
            key = profilerResult.MethodName;
            return profilerResult.Category == ProfilerCategory.UpdateNetworkEvent;
        }
    }
}