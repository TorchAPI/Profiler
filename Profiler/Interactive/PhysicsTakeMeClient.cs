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
        const string GpsNamePrefix = "Physics Profiler: ";

        readonly List<PhysicsEntitySnapshot[]> _clusters;

        public PhysicsTakeMeClient()
        {
            _clusters = new List<PhysicsEntitySnapshot[]>();
        }

        public void Update(IEnumerable<HkWorld> clusters)
        {
            _clusters.Clear();
            foreach (var cluster in clusters)
            {
                var entities = cluster
                    .GetEntities()
                    .Select(e => PhysicsEntitySnapshot.TryCreate(e))
                    .Unwrap()
                    .ToArray();

                _clusters.Add(entities);
            }
        }

        public IEnumerable<PhysicsEntitySnapshot> GetEntitiesAt(int index)
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
            var (_, center) = VRageUtils.GetBound(entities.Select(e => e.Position));

            player.Character.SetPosition(center);
            DeleteGpss(player.IdentityId);

            await VRageUtils.MoveToGameLoop(); // to create gps entities

            foreach (var grid in entities)
            {
                var gps = CreateGridGps($"{GpsNamePrefix}{grid.Name}", grid.Position, "", Color.Purple);
                MySession.Static.Gpss.SendAddGpsRequest(player.IdentityId, ref gps);
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
                    MySession.Static.Gpss.SendDeleteGpsRequest(identityId, gps.Hash);
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