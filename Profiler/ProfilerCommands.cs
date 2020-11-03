using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Profiler
{
    [Category("profile")]
    public class ProfilerCommands : CommandModule
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly GpsSendClient _gpsSendClient = new GpsSendClient();

        const string HelpText = "--secs=SampleLength --top=ReportEntries --faction=Tag --player=PlayerName --this --gps";

        [Command("blocktypes", "Profiles performance per block type", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileBlockType()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new BlockTypeProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block type, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Take(args.Top)
                        .Select(p => (BlockTypeToString(p.Key), p.ProfilerEntry));

                    Respond(totalTicks, data);
                }
            });
        }

        static string BlockTypeToString(Type type)
        {
            return type.ToString().Split('.').LastOrDefault() ?? "unknown";
        }

        [Command("blocks", "Profiles performance per block definition", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileBlock()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new BlockDefinitionProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block definition, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Take(args.Top)
                        .Select(p => (p.Key.BlockPairName, p.ProfilerEntry));

                    Respond(totalTicks, data);
                }
            });
        }

        [Command("grids", "Profiles performance per grid", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileGrids()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new GridProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling grids, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var gridProfilerEntries = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Where(p => !p.Key.Closed)
                        .Take(args.Top);

                    var data = gridProfilerEntries.Select(p => (p.Key.DisplayName, p.ProfilerEntry));
                    Respond(totalTicks, data);

                    if (args.SendGpsToPlayer)
                    {
                        _gpsSendClient.CleanGPS(args.PlayerIdToSendGps);

                        foreach (var (grid, profilerEntry) in gridProfilerEntries)
                        {
                            var gpsName = $"{grid.DisplayName} ({(double) profilerEntry.TotalTimeMs / totalTicks:0.0000}ms/f)";
                            var gpsPosition = grid.PositionComp.WorldAABB.Center;
                            _gpsSendClient.SendGps(args.PlayerIdToSendGps, gpsName, gpsPosition);
                        }
                    }
                }
            });
        }

        [Command("factions", "Profiles performance per faction", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileFactions()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new FactionProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling factions, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Where(p => p.Key != null)
                        .Take(args.Top)
                        .Select(p => (p.Key.Tag, p.ProfilerEntry));

                    Respond(totalTicks, data);
                }
            });
        }

        [Command("players", "Profiles performance per player", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfilePlayers()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new PlayerProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling players, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Where(p => p.Key != null)
                        .Select(p => (p.Key.DisplayName, p.ProfilerEntry))
                        .Take(args.Top);

                    Respond(totalTicks, data);
                }
            });
        }

        [Command("scripts", "Profiles performance of programmable blocks")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileScripts()
        {
            RunThread(async () =>
            {
                var args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(args.PlayerMask, args.GridMask, args.FactionMask);

                using (var profiler = new UserScriptProfiler(mask))
                using (ProfilerPatch.Profile(profiler))
                {
                    Context.Respond($"Started profiling scripts, result in {args.Seconds}s");

                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    await Task.Delay(TimeSpan.FromSeconds(args.Seconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;
                    var profilerEntities = profiler.GetProfilerEntries();

                    var data = profilerEntities
                        .OrderByDescending(p => p.ProfilerEntry.TotalTimeMs)
                        .Where(p => !p.Key.Closed)
                        .Take(args.Top)
                        .Select(p => (PbToString(p.Key), p.ProfilerEntry));

                    Respond(totalTicks, data);
                }
            });
        }

        static string PbToString(MyProgrammableBlock pb)
        {
            var blockName = pb.DisplayName;
            var gridName = pb.GetParentEntityOfType<MyCubeGrid>()?.DisplayName ?? "<none>";
            return $"'{blockName}' (in '{gridName}')";
        }

        static void RunThread(Func<Task> task)
        {
            Task.Factory.StartNew(task).Forget(Log);
        }
        
        void Respond(ulong totalTicks, IEnumerable<(string, ProfilerEntry)> data)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Finished profiling, {totalTicks} ticks past");

            foreach (var (name, profilerEntry) in data)
            {
                var totalTime = $"{profilerEntry.TotalTimeMs:0.00}ms";
                var mainThreadTime = $"{(double) profilerEntry.TotalMainThreadTimeMs / totalTicks:0.00}ms/f";
                var offThreadTime = $"{(double) profilerEntry.TotalOffThreadTimeMs / totalTicks:0.00}ms/f";
                messageBuilder.AppendLine($"'{name}' took {mainThreadTime} main, {offThreadTime} parallel (total {totalTime}");
            }

            Context.Respond(messageBuilder.ToString());
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
    }
}