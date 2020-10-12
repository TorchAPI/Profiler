using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.World;

namespace Profiler.Interactive
{
    public sealed class FactionProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<long, ProfilerEntry> _profilerEntries;
        readonly Func<long, ProfilerEntry> _makeProfilerEntry;

        public FactionProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<long, ProfilerEntry>();
            _makeProfilerEntry = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(long FactionId, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value));
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            var gameEntity = profilerResult.GetGameEntity();
            var player = _mask.ExtractPlayer(gameEntity);
            if (!player.HasValue) return;

            var faction = MySession.Static.Factions.TryGetPlayerFaction(player.Value);
            if (faction == null) return;

            var profilerEntry = _profilerEntries.GetOrAdd(faction.FactionId, _makeProfilerEntry);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}