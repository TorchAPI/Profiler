using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Profiler.Core
{
    public class SlimProfilerEntry
    {
        private ulong _passes;
        private ulong _totalTime;
        private long _watchStartTime;
        private int _watchStarts;

        private ulong _startTick;
        private int _activeCount;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Start()
        {
            if (Interlocked.Add(ref _watchStarts, 1) == 1)
            {
                _watchStartTime = Stopwatch.GetTimestamp();
                _passes++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Stop()
        {
            Debug.Assert(_watchStarts > 0);
            if (Interlocked.Add(ref _watchStarts, -1) == 0)
                _totalTime += (ulong) (Stopwatch.GetTimestamp() - _watchStartTime);
        }

        internal bool IsActive
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _activeCount > 0;
        }

        internal void PushProfiler(ulong tickId)
        {
            if (Interlocked.Add(ref _activeCount, 1) != 1) return;
            _totalTime = 0;
            _startTick = tickId;
            _passes = 0;
        }

        /// <summary>
        /// Returns time per tick, in ms
        /// </summary>
        /// <param name="tickId"></param>
        /// <param name="hits">hits per tick</param>
        /// <returns></returns>
        internal double PopProfiler(ulong tickId, out double hits)
        {
            Debug.Assert(_activeCount > 0);
            Interlocked.Add(ref _activeCount, -1);
            var deltaTicks = (double) unchecked(tickId - _startTick);
            hits = _passes / deltaTicks;
            var loadTimeMs = _totalTime * 1000D / Stopwatch.Frequency;
            return loadTimeMs / deltaTicks;
        }
    }
}