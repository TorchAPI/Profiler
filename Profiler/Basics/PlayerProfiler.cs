using System.Collections.Generic;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class PlayerProfiler : BaseProfiler<MyIdentity>
    {
        readonly GameEntityMask _mask;

        public PlayerProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyIdentity> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;
            if (profilerResult.GameEntity is not IMyEntity entity) return;

            if (entity is MyCubeGrid grid)
            {
                if (_mask.TestAll(grid))
                {
                    foreach (var ownerId in grid.BigOwners)
                    {
                        if (MySession.Static.Players.TryGetIdentity(ownerId) is { } player)
                        {
                            acceptedKeys.Add(player);
                        }
                    }
                }

                return;
            }

            /* technically "correct" but players can't do anything about this
            if (entity is MyCharacter character)
            {
                var playerId = character.GetPlayerIdentityId();
                if (MySession.Static.Players.TryGetIdentity(playerId) is { } player)
                {
                    if (_mask.TestPlayer(player))
                    {
                        acceptedKeys.Add(player);
                    }
                }

                return;
            }
            */

            if (entity.GetParentEntityOfType<MyCubeBlock>() is { } block)
            {
                if (_mask.TestAll(block))
                {
                    if (MySession.Static.Players.TryGetIdentity(block.OwnerId) is { } player)
                    {
                        acceptedKeys.Add(player);
                    }
                }

                return;
            }

            //todo
        }
    }
}