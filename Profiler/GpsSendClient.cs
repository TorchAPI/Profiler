using System.Collections.Concurrent;
using System.Collections.Generic;
using Sandbox.Game.Screens.Helpers;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Profiler
{
    public sealed class GpsSendClient
    {
        readonly ConcurrentDictionary<long, HashSet<int>> GpsForIdentity;

        public GpsSendClient()
        {
            GpsForIdentity = new ConcurrentDictionary<long, HashSet<int>>();
        }

        public void SendGps(long player, string name, Vector3 position)
        {
            var gps = new MyGps(new MyObjectBuilder_Gps.Entry
            {
                name = name,
                DisplayName = name,
                coords = position,
                showOnHud = true,
                color = Color.Purple,
                description = "",
                entityId = 0,
                isFinal = false
            });

            MyAPIGateway.Session?.GPS.AddGps(player, gps);

            var set = GpsForIdentity.GetOrAdd(player, x => new HashSet<int>());

            lock (set)
            {
                set.Add(gps.Hash);
            }
        }

        public void CleanGPS(long gpsId)
        {
            if (!GpsForIdentity.TryGetValue(gpsId, out var data)) return;

            var e = MyAPIGateway.Session?.GPS.GetGpsList(gpsId);
            if (e == null) return;

            lock (data)
            {
                foreach (var k in data)
                {
                    IMyGps existing = null;
                    foreach (var ex in e)
                        if (ex.Hash == k)
                        {
                            existing = ex;
                            break;
                        }

                    if (existing?.DiscardAt != null)
                    {
                        MyAPIGateway.Session.GPS.RemoveGps(gpsId, existing.Hash);
                    }
                }

                data.Clear();
            }
        }
    }
}