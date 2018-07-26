using System;
using Profiler.Core;
using Sandbox.Definitions;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.World;
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
        private const string HelpText = "--ticks=SampleLength --top=ReportEntries --faction=Tag --player=PlayerName";

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

        [Command("help", "Reports output format")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Help()
        {
            Context.Respond("Use --player=PlayerName to report values for a single player");
            Context.Respond("Use --faction=FactionTag to report values for a single faction");
            Context.Respond("Results are reported as entry description, milliseconds per tick (updates per tick)");
        }

        private void Handle(ProfilerRequestType type)
        {
            var ticks = SampleTicks;
            var top = Top;
            long? factionMask = null;
            long? playerMask = null;
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

            if (!ProfilerData.ChangeMask(playerMask, factionMask, null, null))
            {
                Context.Respond($"Failed to change profiling mask.  There can only be one.");
                return;
            }

            var req = new ProfilerRequest(type, ticks);
            var context = Context;
            req.OnFinished += (results) =>
            {
                for (var i = 0; i < Math.Min(top, results.Length); i++)
                    context.Respond($"{results[i].Name}: {FormatTime(results[i].MsPerTick)} ({results[i].HitsPerTick:F1} upt)");
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