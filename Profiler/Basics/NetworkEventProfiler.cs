using System.Collections.Generic;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class NetworkEventProfiler : BaseProfiler<string>
    {
        protected override void Accept(in ProfilerResult profilerResult, ICollection<string> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.UpdateNetworkEvent) return;

            var methodName = profilerResult.MethodName;
            acceptedKeys.Add(methodName);
        }
    }
}