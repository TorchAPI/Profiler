using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Profiler.Core;
using Profiler.Interactive;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using TorchUtils.Utils;
using VRage.Game.ModAPI;

namespace Profiler
{
    [Category("profile")]
    public class ProfilerCommands : CommandModule
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly GpsSendClient _gpsSendClient = new GpsSendClient();

        const string HelpText = "--ticks=SampleLength --top=ReportEntries --faction=Tag --player=PlayerName --this --gps";

        [Command("blocktypes", "Profiles performance per block type", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void BlockType()
        {
            RunThread(async () =>
            {
                Context.Respond("Started profiling by block type");

                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new BlockTypeProfiler(mask))
                using (ProfilerPatch.AddObserverUntilDisposed(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Take(args.Top)
                        .Select(p => (p.Type.ToString(), p.ProfilerEntry));

                    Respond(args.Seconds, totalTicks, data);
                }
            });
        }

        [Command("blocks", "Profiles performance per block definition", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Block()
        {
            RunThread(async () =>
            {
                Context.Respond("Started profiling by block definition");

                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new BlockDefinitionProfiler(mask))
                using (ProfilerPatch.AddObserverUntilDisposed(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Take(args.Top)
                        .Select(p => (p.BlockDefinition.BlockPairName, p.ProfilerEntry));

                    Respond(args.Seconds, totalTicks, data);
                }
            });
        }

        [Command("grids", "Profiles performance per grid", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Grids()
        {
            RunThread(async () =>
            {
                Context.Respond("Started profiling grids");

                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new GridProfiler(mask))
                using (ProfilerPatch.AddObserverUntilDisposed(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var gridProfilerEntries = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Select(p => (Grid: (MyCubeGrid) MyEntities.GetEntityById(p.GridId), p.ProfilerEntry))
                        .Where(p => p.Grid != null)
                        .Take(args.Top)
                        .ToArray();

                    var data = gridProfilerEntries.Select(p => (p.Grid.DisplayName, p.ProfilerEntry));
                    Respond(args.Seconds, totalTicks, data);

                    if (args.SendGpsToPlayer)
                    {
                        _gpsSendClient.CleanGPS(args.PlayerIdToSendGps);

                        foreach (var (grid, profilerEntry) in gridProfilerEntries)
                        {
                            var name = $"{grid.DisplayName} ({(double) profilerEntry.TotalTimeMs / totalTicks:0.00}ms/f)";
                            var position = grid.PositionComp.WorldAABB.Center;
                            _gpsSendClient.SendGps(args.PlayerIdToSendGps, name, position);
                        }
                    }
                }
            });
        }

        [Command("factions", "Profiles performance per faction", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Factions()
        {
            RunThread(async () =>
            {
                Context.Respond("Started profiling factions");

                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new FactionProfiler(mask))
                using (ProfilerPatch.AddObserverUntilDisposed(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();
                    
                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Select(p => (Faction: MySession.Static.Factions.TryGetFactionById(p.FactionId), p.ProfilerEntry))
                        .Where(p => p.Faction != null)
                        .Select(p => (p.Faction.Tag, p.ProfilerEntry))
                        .Take(args.Top);

                    Respond(args.Seconds, totalTicks, data);
                }
            });
        }

        [Command("players", "Profiles performance per player", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Players()
        {
            RunThread(async () =>
            {
                Context.Respond("Started profiling players");

                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new PlayerProfiler(mask))
                using (ProfilerPatch.AddObserverUntilDisposed(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Select(p => (Player: MySession.Static.Players.TryGetIdentity(p.PlayerId), p.ProfilerEntry))
                        .Where(p => p.Player != null)
                        .Select(p => (p.Player.DisplayName, p.ProfilerEntry))
                        .Take(args.Top);

                    Respond(args.Seconds, totalTicks, data);
                }
            });
        }

        static void RunThread(Func<Task> task)
        {
            Task.Factory.StartNew(task).Forget(Log);
        }

        void Respond(ulong totalSeconds, ulong totalTicks, IEnumerable<(string, ProfilerEntry)> data)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Finished profiling for {totalSeconds}s");

            foreach (var (name, profilerEntry) in data)
            {
                var mainThreadTime = $"{(double) profilerEntry.TotalMainThreadTimeMs / totalTicks:0.00}ms/f";
                var offThreadTime = $"{(double) profilerEntry.TotalOffThreadTimeMs / totalTicks:0.00}ms/f";
                messageBuilder.AppendLine($"'{name}' took {mainThreadTime} main, {offThreadTime} parallel");
            }

            Context.Respond(messageBuilder.ToString());
        }

        [Command("scripts", "Profiles performance of programmable blocks")]
        [Permission(MyPromoteLevel.Moderator)]
        public void Scripts()
        {
        }

        [Command("cleangps", "Cleans GPS markers created by the profiling system")]
        [Permission(MyPromoteLevel.Moderator)]
        public void CleanGps()
        {
            var controlled = Context.Player;
            if (controlled == null)
            {
                Context.Respond("GPS clean can only be used by players");
                return;
            }

            _gpsSendClient.CleanGPS(controlled.IdentityId);
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

        [Command("dbstop", "Stops database reporting")]
        [Permission(MyPromoteLevel.Moderator)]
        public void DbStop()
        {
            var plugin = (ProfilerPlugin) Context.Plugin;
            plugin.StopDbReporting();
        }

        [Command("dbrestart", "Restarts database reporting")]
        [Permission(MyPromoteLevel.Moderator)]
        public void DbRestart()
        {
            var plugin = (ProfilerPlugin) Context.Plugin;
            plugin.StartDbReporting();
        }
    }
}