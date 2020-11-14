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
        public bool TryGetEntityByKey(K key, out ProfilerEntry entity)
        {
            return _entities.TryGetValue(key, out entity);
        }

        /// <summary>
        /// Gets the entity tagged by the key.
        /// </summary>
        /// <remarks>
        /// Returns null if not found.
        /// </remarks>
        /// <param name="key">Key to search an entity with.</param>
        /// <returns>Entity if found, otherwise null.</returns>
        public ProfilerEntry GetEntityByKey(K key)
        {
            return TryGetEntityByKey(key, out var e) ? e : null;
        }

        /// <summary>
        /// Top entities from the profiler sorted by their total profiled time.
        /// </summary>
        /// <returns>Sorted entities per their profiled time, descending.</returns>
        public IEnumerable<(K Key, ProfilerEntry Entity)> GetTopEntities(int? limit = null)
        {
            return _entities
                .OrderByDescending(r => r.Value.TotalTimeMs)
                .Select(kv => (kv.Key, kv.Value))
                .Take(limit ?? int.MaxValue)
                .ToArray();
        }
    }
}