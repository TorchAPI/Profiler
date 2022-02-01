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

        internal BaseProfilerResult(ulong totalFrameCount, double totalTime, IReadOnlyDictionary<K, ProfilerEntry> self)
        {
            TotalFrameCount = totalFrameCount;
            TotalTime = totalTime;

            // copy here so that we wont have concurrency issues down the road
            // https://stackoverflow.com/questions/11692389
            _entities = self.ToArray().ToDictionary(p => p.Key, p => p.Value);
        }

        /// <summary>
        /// Total game frames the profiler ran.
        /// </summary>
        public ulong TotalFrameCount { get; }

        /// <summary>
        /// Total time the profiler ran.
        /// </summary>
        public double TotalTime { get; }

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
            return TryGet(key, out var e) ? e.MainThreadTime : defaultValue;
        }

        /// <summary>
        /// Top entities from the profiler sorted by their total profiled time.
        /// </summary>
        /// <returns>Sorted entities per their profiled time, descending.</returns>
        public IEnumerable<KeyedEntity> GetTopEntities(int? limit = null)
        {
            //https://stackoverflow.com/questions/11692389
            var entities = _entities.ToArray();
            
            return entities
                .OrderByDescending(r => r.Value.TotalTime)
                .Select(kv => new KeyedEntity(kv.Key, kv.Value))
                .Take(limit ?? int.MaxValue)
                .ToArray();
        }

        /// <summary>
        /// Create new BaseProfilerResult object with mapped keys.
        /// </summary>
        /// <param name="f">Function to map existing keys to new keys.</param>
        /// <typeparam name="K1">New type of keys.</typeparam>
        /// <returns>Object with the same list of entities but with a different key mapping.</returns>
        public BaseProfilerResult<K1> MapKeys<K1>(Func<K, K1> f)
        {
            var mappedEntities = new Dictionary<K1, ProfilerEntry>();
            foreach (var (key, entity) in _entities)
            {
                var newKey = f(key);
                if (mappedEntities.TryGetValue(newKey, out var mappedEntity))
                {
                    mappedEntity.MergeWith(entity);
                }
                else
                {
                    mappedEntities[newKey] = entity;
                }
            }

            return new BaseProfilerResult<K1>(TotalFrameCount, TotalTime, mappedEntities);
        }

        /// <summary>
        /// ValueTuple, basically. Workaround of a .NET cross-version issue, where
        /// Torch is largely on .NET framework v4.6.1 which doesn't support ValueTuple.
        /// </summary>
        /// <remarks>
        /// Don't use as a hash key.
        /// </remarks>
        public readonly struct KeyedEntity
        {
            public readonly K Key;
            public readonly ProfilerEntry Entity;

            public KeyedEntity(K key, ProfilerEntry entity)
            {
                Key = key;
                Entity = entity;
            }

            public override string ToString()
            {
                return $"({Key}, {Entity})";
            }

            // https://docs.microsoft.com/en-us/dotnet/csharp/deconstruct#deconstructing-user-defined-types
            public void Deconstruct(out K key, out ProfilerEntry entity)
            {
                key = Key;
                entity = Entity;
            }
        }
    }
}