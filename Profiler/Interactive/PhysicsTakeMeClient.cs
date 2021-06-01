using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Havok;
using Profiler.Utils;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Profiler.Interactive
{
    public sealed class PhysicsTakeMeClient
    {
        public readonly struct GridSnapshot
        {
            public readonly string Name;
            public readonly Vector3D Position;
            public readonly string FirstOwnerName;
            public readonly string FirstOwnerFactionTag;

            public GridSnapshot(IMyCubeGrid entity)
            {
                Name = entity.DisplayName;
                Position = entity.GetPosition();

                FirstOwnerName = "<null>";
                FirstOwnerFactionTag = "<null>";
                if (entity.BigOwners.TryGetFirst(out var ownerId))
                {
                    var id = MySession.Static.Players.TryGetIdentity(ownerId);
                    FirstOwnerName = id?.DisplayName ?? "<null>";

                    var faction = MySession.Static.Factions.TryGetPlayerFaction(ownerId);
                    FirstOwnerFactionTag = faction?.Tag ?? "<null>";
                }
            }
        }

        const string GpsNamePrefix = "Physics Profiler: ";

        readonly List<GridSnapshot[]> _clusters;

        public PhysicsTakeMeClient()
        {
            _clusters = new List<GridSnapshot[]>();
        }

        public void Update(IEnumerable<HkWorld> clusters)
        {
            _clusters.Clear();
            foreach (var cluster in clusters)
            {
                var grids = cluster
                    .GetEntities()
                    .Where(e => e is IMyCubeGrid)
                    .Cast<IMyCubeGrid>()
                    .Select(g => new GridSnapshot(g))
                    .ToArray();

                _clusters.Add(grids);
            }
        }

        public IEnumerable<GridSnapshot> GetGridsAt(int index)
        {
            return _clusters[index];
        }

        public async Task TakeMe(IMyPlayer player, int index)
        {
            if (player == null)
            {
                throw new ArgumentException("calling player not found");
            }

            var entities = _clusters[index];
            if (entities.Length == 0)
            {
                throw new InvalidOperationException("entities not found");
            }

            var (_, center) = VRageUtils.GetBound(entities.Select(e => e.Position));
            player.Character.SetPosition(center);

            DeleteGpss(player.IdentityId);

            await GameLoopObserver.MoveToGameLoop(); // to create gps entities

            foreach (var grid in entities)
            {
                var gps = CreateGridGps($"{GpsNamePrefix}{grid.Name}", grid.Position, "", Color.Purple);
                MySession.Static.Gpss.SendAddGps(player.IdentityId, ref gps);
            }

            await TaskUtils.MoveToThreadPool();
        }

        public void DeleteGpss(long playerId)
        {
            var allGpss = MySession.Static.Gpss.GetAllGpss();
            foreach (var (identityId, gps) in allGpss)
            {
                if (identityId == playerId && gps.Name.StartsWith(GpsNamePrefix))
                {
                    MySession.Static.Gpss.SendDelete(identityId, gps.Hash);
                }
            }
        }

        static MyGps CreateGridGps(string name, Vector3D position, string description, Color color)
        {
            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = name,
                DisplayName = name,
                coords = position,
                showOnHud = true,
                color = color,
                description = description,
            });

            gps.UpdateHash();

            return gps;
        }
    }
}