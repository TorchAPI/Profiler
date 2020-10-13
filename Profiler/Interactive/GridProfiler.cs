using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Profiler.Core;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

namespace Profiler.Interactive
{
    public sealed class GridProfiler : IProfilerObserver, IDisposable
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<long, ProfilerEntry> _profilerEntries;
        readonly Func<long, ProfilerEntry> _makeProfilerEntry;
        readonly Action<MyEntity> _onGameEntityRemoved;

        public GridProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<long, ProfilerEntry>();
            _makeProfilerEntry = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();

            _onGameEntityRemoved = gameEntity =>
            {
                if (gameEntity == null) return;
                _profilerEntries.Remove(gameEntity.EntityId);
            };

            MyEntities.OnEntityRemove += _onGameEntityRemoved;
        }

        public IEnumerable<(long GridId, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value));
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            var grid = profilerResult.GetParentEntityOfType<MyCubeGrid>();
            if (grid == null) return;
            if (!_mask.AcceptGrid(grid)) return;

            var profilerEntry = _profilerEntries.GetOrAdd(grid.EntityId, _makeProfilerEntry);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);

            MyEntities.OnEntityRemove -= _onGameEntityRemoved;
        }
    }
}