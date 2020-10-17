using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace Profiler.Basics
{
    public sealed class FactionProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<IMyFaction, ProfilerEntry> _profilerEntries;
        readonly Func<IMyFaction, ProfilerEntry> _makeProfilerEntry;

        public FactionProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<IMyFaction, ProfilerEntry>();
            _makeProfilerEntry = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(IMyFaction Faction, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return;
            
            var player = _mask.ExtractPlayer(profilerResult.GameEntity);
            if (!player.HasValue) return;

            var faction = MySession.Static.Factions.TryGetPlayerFaction(player.Value);
            if (faction == null) return;

            var profilerEntry = _profilerEntries.GetOrAdd(faction, _makeProfilerEntry);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}