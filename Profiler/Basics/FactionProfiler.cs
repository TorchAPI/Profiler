using System;
using System.Collections.Generic;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class FactionProfiler : BaseProfiler<IMyFaction>
    {
        readonly GameEntityMask _mask;

        public FactionProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<IMyFaction> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;

            if (entity is MyCubeGrid grid)
            {
                if (_mask.TestAll(grid))
                {
                    foreach (var ownerId in grid.BigOwners)
                    {
                        if (MySession.Static.Factions.TryGetPlayerFaction(ownerId) is { } faction)
                        {
                            acceptedKeys.Add(faction);
                        }
                    }
                }

                return;
            }

            if (entity.GetParentEntityOfType<MyCubeBlock>() is { } block)
            {
                if (_mask.TestAll(block))
                {
                    if (MySession.Static.Factions.TryGetPlayerFaction(block.OwnerId) is { } faction)
                    {
                        acceptedKeys.Add(faction);
                    }
                }

                return;
            }

            //todo
        }
    }
}