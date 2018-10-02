using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.Remoting.Contexts;
using System.Windows.Media;
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

        [Command("mods", "Profiles performance per mod", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Mods()
        {
            Handle(ProfilerRequestType.Mod);
        }

        [Command("session", "Profiles performance per session component", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Session()
        {
            Handle(ProfilerRequestType.Session);
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
            Handle(ProfilerRequestType.Players);
        }

        private static readonly HashSet<ulong> HasRunPhysicsProfiler = new HashSet<ulong>();

        [Command("physics", "Profiles performance per physics world")]
        [Permission(MyPromoteLevel.Admin)]
        public void Physics()
        {
            ulong id = 0;
            if (!Context.SentBySelf)
            {
                var p = Context.Player;
                if (p == null)
                {
                    Context.Respond($"Must have a player.");
                    return;
                }

                id = p.SteamUserId;
            }

            if (HasRunPhysicsProfiler.Add(id))
            {
                Context.Respond(
                    $"Physics world profiling will negatively affect simulation speed while running, and can cause instability.  If you understand the risks, please repeat the command.");
                return;
            }

            Handle(ProfilerRequestType.Physics);
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
            Context.Respond("Add --mod=ModID to report values for a specific mod");
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
            long? entityMask = null;
            MyModContext modFilter = null;
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
                    if (ent == null)
                    {
                        Context.Respond($"Failed to find entity with ID={id}");
                        return;
                    }

                    entityMask = ent.EntityId;
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

                    entityMask = grid.EntityId;
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
                }else if (arg.StartsWith("--mod="))
                {
                    var nam = arg.Substring("--mod=".Length);
                    foreach (var mod in MySession.Static.Mods)
                    {
                        var ctx = new MyModContext();
                        ctx.Init(mod);
                        if (ctx.ModId.Equals(nam, StringComparison.OrdinalIgnoreCase) || ctx.ModId.Equals(nam + ".sbm", StringComparison.OrdinalIgnoreCase) || ctx.ModName.Equals(nam, StringComparison.OrdinalIgnoreCase))
                        {
                            modFilter = ctx;
                            break;
                        }
                    }
                    if (nam.Equals("base", StringComparison.OrdinalIgnoreCase) || nam.Equals("keen", StringComparison.OrdinalIgnoreCase))
                        modFilter = MyModContext.BaseGame;

                    // ReSharper disable once InvertIf
                    if (modFilter == null)
                    {
                        Context.Respond($"Failed to find mod {nam}");
                        return;
                    }
                }

            if (!ProfilerData.ChangeMask(playerMask, factionMask, entityMask, modFilter))
            {
                Context.Respond($"Failed to change profiling mask.  There can only be one.");
                return;
            }

            var req = new ProfilerRequest(type, ticks);
            var context = Context;
            req.OnFinished += (printByPassCount, results) =>
            {
                for (var i = 0; i < Math.Min(top, results.Length); i++)
                {
                    var r = results[i];
                    var formattedTime = FormatTime(r.MsPerTick);
                    var hits = results[i].HitsPerTick;
                    var hitsUnit = results[i].HitsUnit;
                    var formattedName = string.Format(r.Name ?? "unknown", i, formattedTime, hits, hitsUnit);
                    var formattedDesc = string.Format(r.Description ?? "", i, formattedTime, hits, hitsUnit);
                    if (reportGPS.HasValue || !r.Position.HasValue)
                    {
                        context.Respond(printByPassCount
                            ? $"{formattedName} {formattedDesc} took {hits:F1} {hitsUnit}"
                            : $"{formattedName} {formattedDesc} took {formattedTime} ({hits:F1} {hitsUnit})");
                        if (!reportGPS.HasValue || !r.Position.HasValue)
                            continue;
                        var gpsDisplay = printByPassCount ? $"{hits:F1} {hitsUnit} {formattedName}" : $"{formattedTime} {formattedName}";
                        var gpsDesc = formattedDesc + $" {hits:F1} {hitsUnit}";
                        var gps = new MyGps(new MyObjectBuilder_Gps.Entry
                        {
                            name = gpsDisplay,
                            DisplayName = gpsDisplay,
                            coords = r.Position.Value,
                            showOnHud = true,
                            color = VRageMath.Color.Purple,
                            description = gpsDesc,
                            entityId = 0,
                            isFinal = false
                        });
                        MyAPIGateway.Session?.GPS.AddGps(reportGPS.Value, gps);
                        var set = GpsForIdentity.GetOrAdd(reportGPS.Value, (x) => new HashSet<int>());
                        lock (set)
                            set.Add(gps.Hash);
                        continue;
                    }

                    var posData =
                        $"{r.Position.Value.X.ToString(ProfilerRequest.DistanceFormat)},{r.Position.Value.Y.ToString(ProfilerRequest.DistanceFormat)},{r.Position.Value.Z.ToString(ProfilerRequest.DistanceFormat)}";
                    context.Respond(
                        printByPassCount
                            ? $"{formattedName} {formattedDesc} took ({hits:F1} {hitsUnit})  @ {posData}"
                            : $"{formattedName} {formattedDesc} took {formattedTime} ({hits:F1} {hitsUnit})  @ {posData}");
                }

                {
                    var totalUpdates = 0d;
                    var totalTime = 0d;
                    string hitsUnit = null;
                    for (var i = Math.Min(top, results.Length) + 1; i < results.Length; i++)
                    {
                        var r = results[i];
                        totalUpdates += r.HitsPerTick;
                        totalTime += r.MsPerTick;
                        hitsUnit = r.HitsUnit;
                    }

                    if (totalUpdates > 0)
                        context.Respond(printByPassCount
                            ? $"Others took {totalUpdates:F1} {hitsUnit}"
                            : $"Others took {FormatTime(totalTime)} ({totalUpdates:F1} {hitsUnit})");
                }
            };
            var timeEstMs = ticks * MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS * 1000f / (MyMultiplayer.Static?.ServerSimulationRatio ?? 1);
            context.Respond($"Profiling for {type} started, results in {ticks} ticks (about {FormatTime(timeEstMs)})");
            ProfilerData.Submit(req);
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
