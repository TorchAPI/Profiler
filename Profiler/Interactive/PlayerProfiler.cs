using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Profiler.Util;

namespace Profiler.Interactive
{
    public sealed class PlayerProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<long, ProfilerEntry> _profilerEntries;
        readonly Func<long, ProfilerEntry> _makeProfilerEntity;

        public PlayerProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<long, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(long PlayerId, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value));
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            var gameEntity = profilerResult.GetGameEntity();
            var playerOrNull = _mask.ExtractPlayer(gameEntity);
            if (!(playerOrNull is long player)) return;

            var profilerEntry = _profilerEntries.GetOrAdd(player, _makeProfilerEntity);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}