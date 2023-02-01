using System;
using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;
using Utils.Torch;
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

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyCubeGrid> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;

            if (entity is MyCubeGrid grid)
            {
                if (_mask.TestAll(grid))
                {
                    acceptedKeys.Add(grid);
                }

                return;
            }

            if (entity.GetParentEntityOfType<MyCubeGrid>() is { } g)
            {
                if (_mask.TestAll(g))
                {
                    acceptedKeys.Add(g);
                }

                return;
            }

            // todo
        }
    }
}