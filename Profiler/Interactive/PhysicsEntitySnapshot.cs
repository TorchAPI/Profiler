using Utils.General;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.Entities.EnvironmentItems;
using Sandbox.Game.World;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Profiler.Interactive
{
    public readonly struct PhysicsEntitySnapshot
    {
        public readonly string Name;
        public readonly Vector3D Position;
        public readonly string FirstOwnerName;
        public readonly string FirstOwnerFactionTag;

        public static PhysicsEntitySnapshot? TryCreate(IMyEntity entity) => entity switch
        {
            IMyCubeBlock => null,
            MyEntitySubpart => null,
            MySafeZone => null,
            MyVoxelMap => null,
            MyVoxelBase => null,
            IMyFloatingObject => null,
            _ => new PhysicsEntitySnapshot(entity)
        };

        public PhysicsEntitySnapshot(IMyEntity entity)
        {
            Name = $"{entity.GetType().Name} {entity.Name}";
            Position = entity.GetPosition();

            FirstOwnerName = "<null>";
            FirstOwnerFactionTag = "<null>";

            if (entity is IMyCubeGrid grid)
            {
                Name = grid.DisplayName;
                if (grid.BigOwners.TryGetFirst(out var ownerId))
                {
                    var id = MySession.Static.Players.TryGetIdentity(ownerId);
                    FirstOwnerName = id?.DisplayName ?? "<null>";

                    var faction = MySession.Static.Factions.TryGetPlayerFaction(ownerId);
                    FirstOwnerFactionTag = faction?.Tag ?? "<null>";
                }
            }
        }
    }
}