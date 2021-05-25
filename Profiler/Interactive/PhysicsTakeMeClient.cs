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
using VRage.ModAPI;
using VRageMath;

namespace Profiler.Interactive
{
    public sealed class PhysicsTakeMeClient
    {
        const string GpsNamePrefix = "Physics Profiler: ";

        readonly List<IMyEntity[]> _clusters;

        public PhysicsTakeMeClient()
        {
            _clusters = new List<IMyEntity[]>();
        }

        public void Update(IEnumerable<HkWorld> clusters)
        {
            _clusters.Clear();
            foreach (var cluster in clusters)
            {
                var grids = cluster
                    .GetEntities()
                    .Where(e => e is IMyCubeGrid)
                    .ToArray();

                _clusters.Add(grids);
            }
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

            var (_, center) = VRageUtils.GetBound(entities);
            player.Character.SetPosition(center);

            DeleteGpss(player.IdentityId);

            await GameLoopObserver.MoveToGameLoop(); // to create gps entities

            foreach (var grid in entities)
            {
                var gps = CreateGridGps(grid, $"{GpsNamePrefix}{grid.DisplayName}", "", Color.Purple);
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

        static MyGps CreateGridGps(IMyEntity entity, string name, string description, Color color)
        {
            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = name,
                DisplayName = name,
                coords = entity.PositionComp.GetPosition(),
                showOnHud = true,
                color = color,
                description = description,
            });

            gps.SetEntity(entity);
            gps.UpdateHash();

            return gps;
        }
    }
}