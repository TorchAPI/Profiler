using System;
using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;
using Utils.Torch;
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

        protected override void Accept(in ProfilerResult profilerResult, ICollection<Type> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;

            if (entity.GetParentEntityOfType<MyCubeBlock>() is { } block)
            {
                if (_mask.TestAll(block))
                {
                    if (block.BlockDefinition != null)
                    {
                        var blockType = block.GetType();
                        acceptedKeys.Add(blockType);
                    }
                }
            }
        }
    }
}