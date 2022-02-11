using System.Collections.Generic;
using NLog;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;

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

        public bool TestBlock(MyCubeBlock block)
        {
            if (_gridMask.HasValue)
            {
                if (_gridMask != block.Parent.EntityId) return false;
            }

            if (_playerMask.HasValue)
            {
                if (block.OwnerId != _playerMask) return false;
            }

            if (_factionMask.HasValue)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(block.OwnerId);
                if (faction == null) return false;
                if (faction.FactionId != _factionMask) return false;
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

        public bool TestGrid(MyCubeGrid grid)
        {
            if (_gridMask is {} gridMask)
            {
                if (gridMask != grid.EntityId) return false;
            }

            if (_playerMask is {} playerMask)
            {
                if (!grid.BigOwners.Contains(playerMask)) return false;
            }

            if (_factionMask is {} factionMask)
            {
                foreach (var bigOwnerId in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(bigOwnerId);
                    if (faction?.FactionId != factionMask) return false;
                }
            }

            return true;
        }
    }
}