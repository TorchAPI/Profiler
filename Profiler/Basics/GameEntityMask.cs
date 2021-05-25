using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class GameEntityMask
    {
        readonly long? _playerMask;
        readonly long? _gridMask;
        readonly long? _factionMask;

        public static GameEntityMask Empty = new GameEntityMask(null, null, null);

        public GameEntityMask(long? playerMask, long? gridMask, long? factionMask)
        {
            _playerMask = playerMask;
            _gridMask = gridMask;
            _factionMask = factionMask;
        }

        public long? ExtractPlayer(IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                if (!grid.BigOwners.TryGetFirst(out var bigOwner)) return null;
                if (!AcceptGrid(grid)) return null;
                // Use player mask if present instead of the first big owner
                return _playerMask ?? bigOwner;
            }

            var block = entity.GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return null;
            if (!AcceptBlock(block)) return null;

            return block.OwnerId;
        }

        public bool AcceptBlock(MyCubeBlock block)
        {
            if (_gridMask.HasValue && _gridMask != block.Parent.EntityId) return false;
            if (_playerMask.HasValue && block.BuiltBy != _playerMask) return false;
            if (_factionMask.HasValue)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(block.BuiltBy);
                if (faction == null) return false;
                if (faction.FactionId != _factionMask) return false;
            }

            return true;
        }

        public bool AcceptGrid(MyCubeGrid grid)
        {
            if (_gridMask.HasValue && _gridMask != grid.EntityId) return false;
            if (_playerMask.HasValue && !grid.BigOwners.Contains(_playerMask.Value)) return false;
            if (_factionMask.HasValue)
            {
                var good = false;
                foreach (var owner in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(owner);
                    if (faction != null && faction.FactionId == _factionMask)
                    {
                        good = true;
                        break;
                    }
                }

                if (!good) return false;
            }

            return true;
        }

        public bool AcceptEntity(IMyEntity entity)
        {
            return entity switch
            {
                MyCubeGrid grid => AcceptGrid(grid),
                MyCubeBlock block => AcceptBlock(block),
                _ => true,
            };
        }
    }
}