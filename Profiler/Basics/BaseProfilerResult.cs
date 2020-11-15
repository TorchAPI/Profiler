using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace Profiler.Basics
{
    /// <summary>
    /// Receives result from Base Profiler and passes it to frontend code with nice conversions
    /// </summary>
    /// <typeparam name="K">The type of key used in the BaseProfiler.</typeparam>
    public sealed class BaseProfilerResult<K>
    {
        readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly IReadOnlyDictionary<K, ProfilerEntry> _entities;

        internal BaseProfilerResult(ulong totalTicks, TimeSpan totalTime, IReadOnlyDictionary<K, ProfilerEntry> self)
        {
            TotalTicks = totalTicks;
            TotalTime = totalTime;
            _entities = self;
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
        /// Gets the entity tagged by the key.
        /// </summary>
        /// <param name="key">Key to search an entity with.</param>
        /// <param name="entity">Entity if found.</param>
        /// <returns>True if found, otherwise false.</returns>
        public bool TryGet(K key, out ProfilerEntry entity)
        {
            return _entities.TryGetValue(key, out entity);
        }

        public long GetMainThreadMsOrElse(K key, long defaultValue)
        {
            return TryGet(key, out var e) ? e.TotalMainThreadTimeMs : defaultValue;
        }

        /// <summary>
        /// Top entities from the profiler sorted by their total profiled time.
        /// </summary>
        /// <returns>Sorted entities per their profiled time, descending.</returns>
        public IEnumerable<(K Key, ProfilerEntry Entity)> GetTop(int? limit = null)
        {
            return _entities
                .OrderByDescending(r => r.Value.TotalTimeMs)
                .Select(kv => (kv.Key, kv.Value))
                .Take(limit ?? int.MaxValue)
                .ToArray();
        }
    }
}