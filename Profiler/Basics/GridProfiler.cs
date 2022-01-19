using System;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
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
            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;

            key = block.GetParentEntityOfType<MyCubeGrid>();
            return key != null;
        }
    }
}