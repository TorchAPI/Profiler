using System;
using System.Threading;
using Profiler.Core;

namespace Profiler.Basics
{
    public sealed class TotalProfiler : IProfiler
    {
        ulong _startTick;
        DateTime _startTime;
        long _gameTime;

        public ulong Ticks { get; private set; }
        public double TimeMs { get; private set; }
        public double GameTimeMs => _gameTime;

        public void Start()
        {
            _startTick = ProfilerPatch.CurrentTick;
            _startTime = DateTime.UtcNow;
            _gameTime = 0;
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.Category != ProfilerCategory.Total) return;

            Interlocked.Add(ref _gameTime, profilerResult.DeltaTimeMs);
        }

        public void Stop()
        {
            Ticks = ProfilerPatch.CurrentTick - _startTick;
            TimeMs = (DateTime.UtcNow - _startTime).TotalMilliseconds;
        }
    }
}