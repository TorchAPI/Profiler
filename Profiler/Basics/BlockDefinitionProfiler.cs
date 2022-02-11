using System;
using System.Collections.Generic;
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

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyCubeBlockDefinition> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;

            if (entity.GetParentEntityOfType<MyCubeBlock>() is { } block)
            {
                if (_mask.TestAll(block))
                {
                    acceptedKeys.Add(block.BlockDefinition);
                }
            }
        }
    }
}