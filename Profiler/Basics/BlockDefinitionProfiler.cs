using System;
using Profiler.Core;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using TorchUtils;
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

        protected override bool TryAccept(ProfilerResult profilerResult, out MyCubeBlockDefinition key)
        {
            key = null;
            if (profilerResult.Category != ProfilerCategory.General) return false;

            var block = (profilerResult.GameEntity as IMyEntity).GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return false;
            if (!_mask.AcceptBlock(block)) return false;
            if (block.BlockDefinition == null) return false;

            key = block.BlockDefinition;
            return true;
        }
    }
}