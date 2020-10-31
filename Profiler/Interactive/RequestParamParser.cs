using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace Profiler.Interactive
{
    public sealed class RequestParamParser
    {
        const uint DefaultSampleTicks = 10;
        const int DefaultTop = 10;

        public uint Seconds { get; } = DefaultSampleTicks;
        public int Top { get; } = DefaultTop;
        public bool SendGpsToPlayer { get; }
        public long PlayerIdToSendGps { get; }
        public long? PlayerMask { get; }
        public long? GridMask { get; }
        public long? FactionMask { get; }

        public RequestParamParser(IMyPlayer player, IEnumerable<string> arguments)
        {
            var args = new Dictionary<string, string>();
            foreach (var argument in arguments)
            {
                if (!argument.StartsWith("--")) continue;

                var arg = argument.Substring(2, argument.Length - 2);
                var keyValuePair = arg.Split('=');
                var key = keyValuePair[0];
                var value = keyValuePair.Length >= 2 ? keyValuePair[1] : "";
                args[key] = value;
            }

            if (args.TryGetValue("secs", out var tickStr))
            {
                if (!uint.TryParse(tickStr, out var tick))
                {
                    throw new Exception($"Failed to parse tick: '{tickStr}'");
                }

                Seconds = tick;
            }

            if (args.TryGetValue("top", out var topStr))
            {
                if (!int.TryParse(topStr, out var topInput))
                {
                    throw new Exception($"Failed to parse top: '{topStr}'");
                }

                Top = topInput;
            }

            if (args.TryGetValue("gps", out _))
            {
                if (player == null)
                {
                    throw new Exception("GPS return can only be used by players");
                }

                SendGpsToPlayer = true;
                PlayerIdToSendGps = player.IdentityId;
            }

            if (args.TryGetValue("faction", out var factionName))
            {
                if (!ResolveFaction(factionName, out var id))
                {
                    throw new Exception($"Failed to find faction {factionName}");
                }

                FactionMask = id?.FactionId ?? 0;
            }

            if (args.TryGetValue("player", out var playerName))
            {
                if (!ResolveIdentity(playerName, out var id))
                {
                    throw new Exception($"Failed to find player {playerName}");
                }

                PlayerMask = id?.IdentityId ?? 0;
            }

            if (args.TryGetValue("entity", out var entityIdStr))
            {
                if (!long.TryParse(entityIdStr, out var entityId))
                {
                    throw new Exception($"Failed to parse grid ID={entityIdStr}");
                }

                if (!(MyEntities.GetEntityById(entityId) is MyCubeGrid))
                {
                    throw new Exception($"Failed to find grid with ID={entityId}");
                }

                GridMask = entityId;
            }

            if (args.TryGetValue("this", out _))
            {
                var controlled = player?.Controller?.ControlledEntity?.Entity;
                if (controlled == null)
                {
                    throw new Exception("You must have a controlled entity to use the --this argument");
                }

                MyCubeGrid grid;
                var tmp = controlled;
                do
                {
                    grid = tmp as MyCubeGrid;
                    if (grid != null) break;
                    tmp = tmp.Parent;
                } while (tmp != null);

                if (grid == null)
                {
                    throw new Exception("You must be controlling a grid to use the --this argument");
                }

                GridMask = grid.EntityId;
            }
        }

        static bool ResolveIdentity(string name, out MyIdentity id)
        {
            long identityId;
            if (name.StartsWith("sid/", StringComparison.OrdinalIgnoreCase))
            {
                var num = ulong.Parse(name.Substring("sid/".Length));
                identityId = MySession.Static.Players.TryGetIdentityId(num);
            }
            else if (!long.TryParse(name, out identityId))
            {
                foreach (var p in MySession.Static.Players.GetAllPlayers())
                {
                    if (p.SerialId != 0) continue;
                    var identity = MySession.Static.Players.TryGetPlayerIdentity(p);
                    if (identity == null) continue;
                    if (!identity.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                    id = identity;
                    return true;
                }
            }

            id = MySession.Static.Players.TryGetIdentity(identityId);
            return id != null;
        }

        static bool ResolveFaction(string name, out MyFaction faction)
        {
            foreach (var fac in MySession.Static.Factions)
                if (fac.Value.Tag.Equals(name, StringComparison.OrdinalIgnoreCase) || fac.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    fac.Key.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    faction = fac.Value;
                    return true;
                }

            faction = null;
            return name.Equals("nil", StringComparison.OrdinalIgnoreCase) || name.Equals("null", StringComparison.OrdinalIgnoreCase) || name.Equals("0");
        }
    }
}