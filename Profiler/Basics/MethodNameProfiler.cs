using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class MethodNameProfiler : BaseProfiler<string>
    {
        protected override bool TryAccept(ProfilerResult profilerResult, out string key)
        {
            key = profilerResult.MethodName;
            return true;
        }
    }
}