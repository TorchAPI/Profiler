using System;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class BlockDefinitionProfiler : BaseProfiler<MyCubeBlockDefinition>
    {
        readonly GameEntityMask _mask;

        public BlockDefinitionProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyCubeBlockDefinition key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;

            key = block.BlockDefinition;
            return key != null;
        }
    }
}