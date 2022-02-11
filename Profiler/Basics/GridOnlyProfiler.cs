using Profiler.Core;
using Sandbox.Game.Entities;

namespace Profiler.Basics
{
    public sealed class GridOnlyProfiler : BaseProfiler<MyCubeGrid>
    {
        readonly GameEntityMask _mask;

        public GridOnlyProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyCubeGrid key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            if (profilerResult.GameEntity is not MyCubeGrid grid) return false;
            if (!_mask.TestGrid(grid)) return false;

            key = grid;
            return key != null;
        }
    }
}