using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using System.Windows.Media;
using NLog;
using Profiler.Core;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Profiler
{
    [Category("profile")]
    public class ProfilerCommands : CommandModule
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private const ulong SampleTicks = 900;
        private const int Top = 10;
        private const string HelpText = "--ticks=SampleLength --top=ReportEntries --faction=Tag --player=PlayerName --this --gps";

        [Command("blocktypes", "Profiles performance per block type", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void BlockType()
        {
            Handle(ProfilerRequestType.BlockType);
        }

        [Command("blocks", "Profiles performance per block definition", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Block()
        {
            Handle(ProfilerRequestType.BlockDef);
        }

        [Command("grids", "Profiles performance per grid", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Grids()
        {
            Handle(ProfilerRequestType.Grid);
        }

        [Command("factions", "Profiles performance per faction", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Factions()
        {
            Handle(ProfilerRequestType.Faction);
        }

        [Command("players", "Profiles performance per player", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Players()
        {
            Handle(ProfilerRequestType.Player);
        }

        [Command("scripts", "Profiles performance of programmable blocks")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Scripts()
        {
            Handle(ProfilerRequestType.Scripts);
        }

        [Command("cleangps", "Cleans GPS markers created by the profiling system")]
        [Permission(MyPromoteLevel.Moderator)]
        public void CleanGps()
        {
            var controlled = Context.Player;
            if (controlled == null)
            {
                Context.Respond($"GPS clean can only be used by players");
                return;
            }

            CleanGPS(controlled.IdentityId);
        }

        [Command("help", "Reports output format")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Help()
        {
            Context.Respond("Use !longhelp to show all profiler subcommands");
            Context.Respond("Add --player=PlayerName to report values for a single player");
            Context.Respond("Add --faction=FactionTag to report values for a single faction");
            Context.Respond("Add --entity=EntityId to report values for a specific entity");
            Context.Respond("Add --this to profile the entity you're currently controlling (players only)");
            Context.Respond("Add --gps to show positional results as GPS points (players only)");
            Context.Respond("Results are reported as entry description, milliseconds per tick (updates per tick)");
        }

        private void CleanGPS(long gpsId)
        {
            if (!GpsForIdentity.TryGetValue(gpsId, out var data))
                return;
            var e = MyAPIGateway.Session?.GPS.GetGpsList(gpsId);
            if (e == null)
                return;
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

                    if (existing != null && existing.DiscardAt.HasValue)
                        MyAPIGateway.Session.GPS.RemoveGps(gpsId, existing.Hash);
                }

                data.Clear();
            }
        }

        private static readonly ConcurrentDictionary<long, HashSet<int>> GpsForIdentity = new ConcurrentDictionary<long, HashSet<int>>();

        private void Handle(ProfilerRequestType type)
        {
            var ticks = SampleTicks;
            var top = Top;
            long? factionMask = null;
            long? playerMask = null;
            long? gridMask = null;
            long? reportGPS = null;
            foreach (var arg in Context.Args)
                if (arg.StartsWith("--ticks="))
                    ticks = ulong.Parse(arg.Substring("--ticks=".Length));
                else if (arg.StartsWith("--top="))
                    top = int.Parse(arg.Substring("--top=".Length));
                else if (arg.StartsWith("--faction="))
                {
                    var name = arg.Substring("--faction=".Length);
                    if (!ResolveFaction(name, out var id))
                    {
                        Context.Respond($"Failed to find faction {name}");
                        return;
                    }

                    factionMask = id?.FactionId ?? 0;
                }
                else if (arg.StartsWith("--player="))
                {
                    var name = arg.Substring("--player=".Length);
                    if (!ResolveIdentity(name, out var id))
                    {
                        Context.Respond($"Failed to find player {name}");
                        return;
                    }

                    playerMask = id?.IdentityId ?? 0;
                }
                else if (arg.StartsWith("--entity="))
                {
                    var id = long.Parse(arg.Substring("--entity=".Length));
                    var ent = MyEntities.GetEntityById(id);
                    if (!(ent is MyCubeGrid))
                    {
                        Context.Respond($"Failed to find grid with ID={id}");
                        return;
                    }

                    gridMask = ent.EntityId;
                }
                else if (arg == "--this")
                {
                    var controlled = Context.Player?.Controller?.ControlledEntity?.Entity;
                    if (controlled == null)
                    {
                        Context.Respond($"You must have a controlled entity to use the --this argument");
                        return;
                    }

                    MyCubeGrid grid;
                    var tmp = controlled;
                    do
                    {
                        grid = tmp as MyCubeGrid;
                        if (grid != null)
                            break;
                        tmp = tmp.Parent;
                    } while (tmp != null);

                    if (grid == null)
                    {
                        Context.Respond($"You must be controlling a grid to use the --this argument");
                        return;
                    }

                    gridMask = grid.EntityId;
                }
                else if (arg == "--gps")
                {
                    var controlled = Context.Player;
                    if (controlled == null)
                    {
                        Context.Respond($"GPS return can only be used by players");
                        return;
                    }

                    reportGPS = controlled.IdentityId;
                    CleanGPS(reportGPS.Value);
                }

            var req = new ProfilerRequest(type, ticks);
            var context = Context;
            req.OnFinished += (results) =>
            {
                for (var i = 0; i < Math.Min(top, results.Length); i++)
                {
                    var r = results[i];
                    var mainThreadTime = FormatTime(r.MainThreadMsPerTick);
                    var offThreadTime = FormatTime(r.OffThreadMsPerTick);
                    if (reportGPS.HasValue && r.Position.HasValue)
                    {
                        var gpsDisplay = $"{mainThreadTime} / {offThreadTime}: {r.Name}";
                        var gps = new MyGps(new MyObjectBuilder_Gps.Entry
                        {
                            name = gpsDisplay,
                            DisplayName = gpsDisplay,
                            coords = r.Position.Value,
                            showOnHud = true,
                            color = VRageMath.Color.Purple,
                            description = "",
                            entityId = 0,
                            isFinal = false
                        });
                        MyAPIGateway.Session?.GPS.AddGps(reportGPS.Value, gps);
                        var set = GpsForIdentity.GetOrAdd(reportGPS.Value, (x) => new HashSet<int>());
                        lock (set)
                            set.Add(gps.Hash);
                        continue;
                    }

                    var msg = $"{r.Name} {r.Description} took {mainThreadTime} main, {offThreadTime} parallel";
                    if (r.Position.HasValue)
                    {
                        msg += " @ " +
                               r.Position.Value.X.ToString(ProfilerRequest.DistanceFormat) + "," +
                               r.Position.Value.Y.ToString(ProfilerRequest.DistanceFormat) + "," +
                               r.Position.Value.Z.ToString(ProfilerRequest.DistanceFormat);
                    }

                    Log.Debug(msg);
                    context.Respond(msg);
                }

                {
                    var otherCount = 0;
                    var totalMainThreadTime = 0d;
                    var totalOffThreadTime = 0d;
                    string hitsUnit = null;
                    for (var i = Math.Min(top, results.Length) + 1; i < results.Length; i++)
                    {
                        var r = results[i];
                        otherCount++;
                        totalMainThreadTime += r.MainThreadMsPerTick;
                        totalOffThreadTime += r.OffThreadMsPerTick;
                    }

                    if (otherCount > 0)
                    {
                        var msg = $"Others took {FormatTime(totalMainThreadTime)} main, {FormatTime(totalOffThreadTime)} parallel";
                        context.Respond(msg);
                        Log.Debug(msg);
                    }
                }
                var finishMsg = $"Finished profiling {req.Type} for {req.SamplingTicks} ticks";
                context.Respond(finishMsg);
                Log.Debug(finishMsg);
            };

            var timeEstMs = ticks * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS * 1000f / (MyMultiplayer.Static?.ServerSimulationRatio ?? 1);
            if (!ProfilerData.Submit(req, gridMask, playerMask, factionMask))
                Context.Respond("Profiler is already active.  Only one profiling command can be active at a time");
            else
            {
                context.Respond($"Profiling for {type} started, results in {ticks} ticks (about {FormatTime(timeEstMs)})");
                Log.Debug($"Start profiling {req.Type} for {req.SamplingTicks} ticks");
            }
        }

        private static string FormatTime(double ms)
        {
            if (ms > 1000)
                return $"{ms / 1000:F0}s";
            if (ms > 1)
                return $"{ms:F0}ms";
            ms *= 1000;
            if (ms > 1)
                return $"{ms:F0}us";
            ms *= 1000;
            return $"{ms:F0}ns";
        }

        private static bool ResolveIdentity(string name, out MyIdentity id)
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
                    if (p.SerialId != 0)
                        continue;
                    var identity = MySession.Static.Players.TryGetPlayerIdentity(p);
                    if (identity == null)
                        continue;
                    if (!identity.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
                    id = identity;
                    return true;
                }
            }

            id = MySession.Static.Players.TryGetIdentity(identityId);
            return id != null;
        }

        private static bool ResolveFaction(string name, out MyFaction faction)
        {
            foreach (var fac in MySession.Static.Factions)
                if (fac.Value.Tag.Equals(name, StringComparison.OrdinalIgnoreCase) || fac.Value.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    fac.Key.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    faction = fac.Value;
                    return true;
                }

            faction = null;
            return (name.Equals("nil", StringComparison.OrdinalIgnoreCase) || name.Equals("null", StringComparison.OrdinalIgnoreCase) || name.Equals("0"));
        }
    }
}