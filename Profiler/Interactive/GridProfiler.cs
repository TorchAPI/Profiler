using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

namespace Profiler.Interactive
{
    public sealed class GridProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<MyCubeGrid, ProfilerEntry> _profilerEntries;
        readonly Func<MyCubeGrid, ProfilerEntry> _makeProfilerEntry;
        readonly Action<MyEntity> _onGameEntityRemoved;

        public GridProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<MyCubeGrid, ProfilerEntry>();
            _makeProfilerEntry = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();

            _onGameEntityRemoved = gameEntity =>
            {
                if (!(gameEntity is MyCubeGrid grid)) return;
                _profilerEntries.Remove(grid);
            };

            MyEntities.OnEntityRemove += _onGameEntityRemoved;
        }

        public IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.Entrypoint != Entrypoint.General) return;
            
            var grid = profilerResult.GameEntity.GetParentEntityOfType<MyCubeGrid>();
            if (grid == null) return;
            if (!_mask.AcceptGrid(grid)) return;

            var profilerEntry = _profilerEntries.GetOrAdd(grid, _makeProfilerEntry);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);

            MyEntities.OnEntityRemove -= _onGameEntityRemoved;
        }
    }
}