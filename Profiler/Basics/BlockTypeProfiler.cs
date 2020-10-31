using System;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.Entities;

namespace Profiler.Basics
{
    public sealed class BlockTypeProfiler : BaseProfiler<Type>
    {
        readonly GameEntityMask _mask;

        public BlockTypeProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(ProfilerResult profilerResult, out Type key)
        {
            key = null;
            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return false;

            var block = profilerResult.GameEntity.GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return false;
            if (!_mask.AcceptBlock(block)) return false;
            if (block.BlockDefinition == null) return false;

            key = block.GetType();
            return true;
        }
    }
}