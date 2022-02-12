using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;

namespace Profiler.Basics
{
    public sealed class GridOnlyProfiler : BaseProfiler<MyCubeGrid>
    {
        readonly GameEntityMask _mask;

        public GridOnlyProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyCubeGrid> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;

            if (profilerResult.GameEntity is MyCubeGrid grid)
            {
                if (_mask.TestAll(grid))
                {
                    acceptedKeys.Add(grid);
                }
            }
        }
    }
}