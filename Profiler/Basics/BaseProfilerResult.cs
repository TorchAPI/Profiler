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
        readonly IReadOnlyDictionary<K, ProfilerEntry> _entities;

        internal BaseProfilerResult(ulong totalFrameCount, TimeSpan totalTime, IReadOnlyDictionary<K, ProfilerEntry> self)
        {
            TotalFrameCount = totalFrameCount;
            TotalTime = totalTime;
            _entities = self;
        }

        /// <summary>
        /// Total game frames the profiler ran.
        /// </summary>
        public ulong TotalFrameCount { get; }

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

        public double GetMainThreadTickMsOrElse(K key, long defaultValue)
        {
            return TryGet(key, out var e) ? e.TotalMainThreadTime : defaultValue;
        }

        /// <summary>
        /// Top entities from the profiler sorted by their total profiled time.
        /// </summary>
        /// <returns>Sorted entities per their profiled time, descending.</returns>
        public IEnumerable<(K Key, ProfilerEntry Entity)> GetTopEntities(int? limit = null)
        {
            return _entities
                .OrderByDescending(r => r.Value.TotalTime)
                .Select(kv => (kv.Key, kv.Value))
                .Take(limit ?? int.MaxValue)
                .ToArray();
        }

        /// <summary>
        /// Create new BaseProfilerResult object with mapped keys.
        /// </summary>
        /// <param name="f">Function to map existing keys to new keys.</param>
        /// <typeparam name="K1">New type of keys.</typeparam>
        /// <returns>Object with the same list of entities but with a different key mapping.</returns>
        public BaseProfilerResult<K1> Select<K1>(Func<K, K1> f)
        {
            var entities = _entities
                .Select(kv => (f(kv.Key), kv.Value))
                .ToDictionary(kv => kv.Item1, kv => kv.Item2);

            return new BaseProfilerResult<K1>(TotalFrameCount, TotalTime, entities);
        }
    }
}