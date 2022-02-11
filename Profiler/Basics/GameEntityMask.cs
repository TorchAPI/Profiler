using System.Collections.Generic;
using NLog;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class GameEntityMask
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        public static readonly GameEntityMask Empty = new GameEntityMask(null, null, null, null);

        readonly long? _playerMask;
        readonly long? _gridMask;
        readonly long? _factionMask;
        readonly ISet<string> _exemptBlocks;

        public GameEntityMask(long? playerMask = null, long? gridMask = null, long? factionMask = null, IEnumerable<string> exemptBlockTypeIds = null)
        {
            _playerMask = playerMask;
            _gridMask = gridMask;
            _factionMask = factionMask;
            _exemptBlocks = exemptBlockTypeIds?.ToSet();
        }

        public bool TestAll(MyCubeBlock block)
        {
            if (_gridMask is { } gridMask)
            {
                if (gridMask != block.Parent.EntityId) return false;
            }

            if (_playerMask is { } playerMask)
            {
                if (block.OwnerId != playerMask) return false;
            }

            if (_factionMask is { } factionMask)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(block.OwnerId);
                if (faction?.FactionId != factionMask) return false;
            }

            if (_exemptBlocks is { Count: > 0 })
            {
                var blockTypeId = BlockTypeIdPool.Instance.GetTypeId(block);
                if (_exemptBlocks.Contains(blockTypeId))
                {
                    return false;
                }
            }

            return true;
        }

        public bool TestAll(MyCubeGrid grid)
        {
            if (_gridMask is { } gridMask)
            {
                if (gridMask != grid.EntityId) return false;
            }

            if (_playerMask is { } playerMask)
            {
                if (!grid.BigOwners.Contains(playerMask)) return false;
            }

            if (_factionMask is { } factionMask)
            {
                foreach (var bigOwnerId in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(bigOwnerId);
                    if (faction?.FactionId != factionMask) return false;
                }
            }

            return true;
        }

        public bool TestAll(MyIdentity player)
        {
            if (_playerMask is { } playerMask)
            {
                if (player.IdentityId != playerMask) return false;
            }

            if (_factionMask is { } factionMask)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(player.IdentityId);
                if (faction?.FactionId != factionMask) return false;
            }

            return true;
        }

        public bool TestAll(MyCharacter character)
        {
            if (character.GetIdentity() is { } id)
            {
                return TestAll(id);
            }

            return true; // not a player
        }

        public bool TestAll(IMyEntity entity) => entity switch
        {
            MyCharacter character => TestAll(character),
            MyCubeGrid grid => TestAll(grid),
            MyCubeBlock block => TestAll(block),
            _ => true, //todo
        };
    }
}