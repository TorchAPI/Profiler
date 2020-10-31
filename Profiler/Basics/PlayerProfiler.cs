using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Sandbox.Game.World;

namespace Profiler.Basics
{
    public sealed class PlayerProfiler : IProfiler, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<MyIdentity, ProfilerEntry> _profilerEntries;
        readonly Func<MyIdentity, ProfilerEntry> _makeProfilerEntity;

        public PlayerProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<MyIdentity, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(MyIdentity Player, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value));
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return;
            
            var playerIdOrNull = _mask.ExtractPlayer(profilerResult.GameEntity);
            if (!(playerIdOrNull is long playerId)) return;

            var identity = MySession.Static.Players.TryGetIdentity(playerId);
            if (identity == null) return;

            var profilerEntry = _profilerEntries.GetOrAdd(identity, _makeProfilerEntity);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}