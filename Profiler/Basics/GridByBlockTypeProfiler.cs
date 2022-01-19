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

            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;
            if (block.BlockDefinition == null) return false;
            if (!block.GetType().Name.Contains(_blockTypeName)) return false;

            key = block.GetParentEntityOfType<MyCubeGrid>();
            return key != null;
        }
    }
}