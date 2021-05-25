using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.Screens.Helpers;
using Torch.Utils;

namespace Profiler.Utils
{
    public static class MyGpsCollection_PlayerGpss
    {
#pragma warning disable 649
        [ReflectedFieldInfo(typeof(MyGpsCollection), "m_playerGpss")]
        static readonly FieldInfo _fieldInfo;
#pragma warning restore 649

        public static Dictionary<long, Dictionary<int, MyGps>> GetPlayerGpss(this MyGpsCollection self)
        {
            return (Dictionary<long, Dictionary<int, MyGps>>) _fieldInfo.GetValue(self);
        }

        public static IEnumerable<(long IdentityId, MyGps Gps)> GetAllGpss(this MyGpsCollection self)
        {
            var allGpss = new List<(long, MyGps)>();
            foreach (var (identity, gpsCollection) in self.GetPlayerGpss())
            foreach (var (_, gps) in gpsCollection)
            {
                allGpss.Add((identity, gps));
            }

            return allGpss;
        }
    }
}