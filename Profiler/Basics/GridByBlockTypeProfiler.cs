using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public class GridByBlockTypeProfiler : BaseProfiler<MyCubeGrid>
    {
        readonly GameEntityMask _mask;
        readonly string _blockTypeName;

        public GridByBlockTypeProfiler(GameEntityMask mask, string blockTypeName)
        {
            _mask = mask;
            _blockTypeName = blockTypeName;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyCubeGrid key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;

            var grid = (profilerResult.GameEntity as IMyEntity).GetParentEntityOfType<MyCubeGrid>();
            if (grid == null) return false;
            if (!_mask.AcceptGrid(grid)) return false;

            var block = (profilerResult.GameEntity as IMyEntity).GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return false;
            if (!_mask.AcceptBlock(block)) return false;
            if (block.BlockDefinition == null) return false;
            if (!block.GetType().Name.Contains(_blockTypeName)) return false;

            key = grid;
            return true;
        }
    }
}