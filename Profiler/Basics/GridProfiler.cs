using System;
using Profiler.Core;
using Sandbox.Game.Entities;
using TorchUtils;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class GridProfiler : BaseProfiler<MyCubeGrid>
    {
        readonly GameEntityMask _mask;

        public GridProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyCubeGrid key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;

            var grid = (profilerResult.GameEntity as IMyEntity).GetParentEntityOfType<MyCubeGrid>();
            if (grid == null) return false;
            if (!_mask.AcceptGrid(grid)) return false;

            key = grid;
            return true;
        }
    }
}