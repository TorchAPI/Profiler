using System.Collections.Generic;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class MethodNameProfiler : BaseProfiler<string>
    {
        protected override void Accept(in ProfilerResult profilerResult, ICollection<string> acceptedKeys)
        {
            var methodName = profilerResult.MethodName;
            acceptedKeys.Add(methodName);
        }
    }
}