using System;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Game.Entities;

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
            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return false;
            
            var block = profilerResult.GameEntity.GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return false;
            if (!_mask.AcceptBlock(block)) return false;
            if (block.BlockDefinition == null) return false;

            key = block.BlockDefinition;
            return true;
        }
    }
}