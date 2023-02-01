using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;
using Utils.Torch;
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

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyCubeGrid> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return;
            if (!_mask.TestAll(block)) return;
            if (block.BlockDefinition == null) return;
            if (!block.GetType().Name.Contains(_blockTypeName)) return;

            var grid = block.GetParentEntityOfType<MyCubeGrid>();
            acceptedKeys.Add(grid);
        }
    }
}