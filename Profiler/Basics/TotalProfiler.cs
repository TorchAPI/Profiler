using System;
using System.Threading;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class TotalProfiler : IProfilerObserver
    {
        long _mainThreadMs;
        long _offThreadMs;

        public long MainThreadMs => _mainThreadMs;
        public long OffThreadMs => _offThreadMs;

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.IsMainThread)
            {
                Interlocked.Add(ref _mainThreadMs, profilerResult.DeltaTimeMs);
            }
            else
            {
                Interlocked.Add(ref _offThreadMs, profilerResult.DeltaTimeMs);
            }
        }
    }
}