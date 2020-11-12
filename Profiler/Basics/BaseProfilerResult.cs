using System;
using System.Collections.Generic;
using System.Linq;

namespace Profiler.Basics
{
    /// <summary>
    /// Receives result from Base Profiler and passes it to frontend code with nice conversions
    /// </summary>
    /// <typeparam name="K">The type of key used in the BaseProfiler.</typeparam>
    public sealed class BaseProfilerResult<K>
    {
        readonly IReadOnlyDictionary<K, ProfilerEntry> _self;

        internal BaseProfilerResult(ulong totalTicks, TimeSpan totalTime, IReadOnlyDictionary<K, ProfilerEntry> self)
        {
            TotalTicks = totalTicks;
            TotalTime = totalTime;
            _self = self;
        }

        /// <summary>
        /// Total tick (game frame) the profiler ran.
        /// </summary>
        public ulong TotalTicks { get; }
        
        /// <summary>
        /// Total time the profiler ran.
        /// </summary>
        public TimeSpan TotalTime { get; }

        /// <summary>
        /// Top entities from the profiler sorted by their total profiled time.
        /// </summary>
        /// <returns>Sorted entities per their profiled time, descending.</returns>
        public IEnumerable<(K Key, ProfilerEntry Entity)> GetTopEntities()
        {
            return _self
                .OrderByDescending(r => r.Value.TotalTimeMs)
                .Select(kv => (kv.Key, kv.Value))
                .ToArray();
        }

        /// <summary>
        /// "Remainder" time, which is a reminder of the sum of all profiled time from the total time the profiler ran.
        /// </summary>
        /// <returns></returns>
        public float GetTotalRemainderTime()
        {
            var sumTime = _self.Sum(s => s.Value.TotalTimeMs);
            var totalTime = (float) TotalTime.TotalMilliseconds;
            var remainTotalTime = totalTime - sumTime;
            return remainTotalTime;
        }

        // sick helper
        public IEnumerable<(string Key, float AverageTimeMs)> GetTopAverageTotalTimesWithRemainder(Func<K, string> keyToStr = null)
        {
            return GetTopEntities()
                .Select(kv => (
                    Key: keyToStr?.Invoke(kv.Key) ?? kv.Key?.ToString() ?? "<null>",
                    AverageTimeMs: (float) kv.Entity.TotalTimeMs / TotalTicks))
                .Append(("<remainder>", GetTotalRemainderTime() / TotalTicks));
        }
    }
}