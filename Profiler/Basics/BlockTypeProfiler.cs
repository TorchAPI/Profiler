using System;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class BlockTypeProfiler : BaseProfiler<Type>
    {
        readonly GameEntityMask _mask;

        public BlockTypeProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out Type key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;
            if (block.BlockDefinition == null) return false;

            key = block.GetType();
            return true;
        }
    }
}