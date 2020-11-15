using System;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class GridProfiler : BaseProfiler<MyCubeGrid>
    {
        readonly GameEntityMask _mask;
        readonly Action<MyEntity> _onGameEntityRemoved;

        public GridProfiler(GameEntityMask mask)
        {
            _mask = mask;

            _onGameEntityRemoved = gameEntity =>
            {
                if (!(gameEntity is MyCubeGrid grid)) return;
                RemoveEntry(grid);
            };

            MyEntities.OnEntityRemove += _onGameEntityRemoved;
        }

        protected override bool TryAccept(ProfilerResult profilerResult, out MyCubeGrid key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;

            var grid = (profilerResult.GameEntity as IMyEntity).GetParentEntityOfType<MyCubeGrid>();
            if (grid == null) return false;
            if (!_mask.AcceptGrid(grid)) return false;

            key = grid;
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            MyEntities.OnEntityRemove -= _onGameEntityRemoved;
        }
    }
}