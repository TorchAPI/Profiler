using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Havok;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Profiler.Interactive;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace Profiler
{
    [Category("profile")]
    public sealed class ProfilerCommands : CommandModule
    {
        const string HelpText = "--secs=SampleLength --top=ReportEntries --faction=Tag --player=PlayerName --this --gps";
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly GpsSendClient _gpsSendClient = new();
        static readonly PhysicsTakeMeClient _takeMeClient = new();

        RequestParamParser _args;

        [Command("on", "Enable profiling", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Enable()
        {
            ProfilerPatch.Enabled = true;
        }

        [Command("off", "Disable profiling", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Disable()
        {
            ProfilerPatch.Enabled = false;
        }

        [Command("blocktypes", "Profiles performance per block type", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileBlockType()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new BlockTypeProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block type, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (b, _) => BlockTypeToString(b));
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
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new BlockDefinitionProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block definition, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (k, _) => k.BlockPairName);
                }
            });
        }

        [Command("grids", "Profiles performance per grid", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileGrids()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new GridProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling grids, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (g, _) => GridToResultText(g));

                    // Sending GPS of laggy grids to caller
                    if (_args.SendGpsToPlayer)
                    {
                        _gpsSendClient.CleanGPS(_args.PlayerIdToSendGps);

                        foreach (var (grid, profilerEntry) in result.GetTopEntities(_args.Top))
                        {
                            var gpsName = $"{grid.DisplayName} ({profilerEntry.TotalTime / result.TotalFrameCount:0.0000}ms/f)";
                            var gpsPosition = grid.PositionComp.WorldAABB.Center;
                            _gpsSendClient.SendGps(_args.PlayerIdToSendGps, gpsName, gpsPosition);
                        }
                    }
                }
            });
        }

        string GridToResultText(MyCubeGrid grid)
        {
            if (!_args.ShowDetails)
            {
                return grid.DisplayName;
            }

            if (!grid.BigOwners.Any())
            {
                return $"{grid.DisplayName} (no owners)";
            }

            var names = new List<string>();

            foreach (var bigOwner in grid.BigOwners)
            {
                var id = MySession.Static.Players.TryGetIdentity(bigOwner);
                if (id == null) continue;

                var faction = MySession.Static.Factions.GetPlayerFaction(bigOwner);

                var playerName = id.DisplayName;
                var factionTag = faction?.Tag ?? "<single>";

                names.Add($"{playerName} [{factionTag}]");
            }

            return $"{grid.DisplayName} ({string.Join(", ", names)})";
        }

        [Command("factions", "Profiles performance per faction", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileFactions()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new FactionProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling factions, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (f, _) => f.Tag);
                }
            });
        }

        [Command("players", "Profiles performance per player", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfilePlayers()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new PlayerProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling players, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (k, _) => k.DisplayName);
                }
            });
        }

        [Command("scripts", "Profiles performance of programmable blocks")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileScripts()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new UserScriptProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling scripts, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (p, _) => PbToString(p));
                }
            });
        }

        static string PbToString(MyProgrammableBlock pb)
        {
            var blockName = pb.DisplayName;
            var gridName = pb.GetParentEntityOfType<MyCubeGrid>()?.DisplayName ?? "<none>";
            return $"'{blockName}' (in '{gridName}')";
        }

        [Command("session", "Profiles performance of session components")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileSession()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                using (var profiler = new SessionComponentsProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling sessions, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));

                    var result = profiler.GetResult();
                    RespondResult(result, (p, _) => p.GetType().Name);
                }
            });
        }

        [Command("physics", "Profiles performance of physics clusters")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfilePhysics()
        {
            this.CatchAndReport(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);
                var physicsParams = new PhysicsParamParser(Context.Args);

                if (physicsParams.InspectIndexOrNull is { } inspectIndex)
                {
                    var msg = new StringBuilder();
                    msg.AppendLine($"List of grids in a cluster at index {inspectIndex}:");

                    var grids = _takeMeClient.GetGridsAt(inspectIndex);
                    foreach (var factions in grids.GroupBy(g => g.FirstOwnerFactionTag))
                    foreach (var grid in factions.OrderBy(g => g.FirstOwnerName))
                    {
                        msg.AppendLine($"[{grid.FirstOwnerFactionTag}] \"{grid.FirstOwnerName}\": \"{grid.Name}\"");
                    }

                    Context.Respond(msg.ToString());
                    return;
                }

                if (physicsParams.TakeMeDone)
                {
                    _takeMeClient.DeleteGpss(Context.Player.IdentityId);
                    Context.Respond("Finished session");
                    return;
                }

                if (physicsParams.TakeMeIndexOrNull is { } takeMeIndex)
                {
                    await _takeMeClient.TakeMe(Context.Player, takeMeIndex);
                    Context.Respond("Move to another cluster by `--takeme=N` or end session by `--takeme=done`");
                    return;
                }

                using (var profiler = new PhysicsProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Log.Warn("Physics profiling needs to sync all threads! This may cause performance impact.");
                    Context.Respond($"Started profiling clusters, result in {physicsParams.Tics} frames (--tics=N)");

                    await GameLoopObserver.MoveToGameLoop();

                    profiler.MarkStart();

                    for (var _ = 0; _ < physicsParams.Tics; _++)
                    {
                        await GameLoopObserver.MoveToGameLoop();
                    }

                    profiler.MarkEnd();

                    await TaskUtils.MoveToThreadPool();

                    var result = profiler.GetResult();
                    RespondResult(result, (w, i) => GetWorldName(w, i, mask));

                    Context.Respond("Teleport to a cluster by `--takeme=N`");
                    Context.Respond("Show a list of grids by `--inspect=N`");

                    var topClusters = result.GetTopEntities(5).Select(e => e.Key).ToArray();
                    _takeMeClient.Update(topClusters);
                }
            });
        }

        static string GetWorldName(HkWorld world, int index, GameEntityMask mask)
        {
            var entities = world
                .GetEntities()
                .Where(e => mask.AcceptEntity(e))
                .ToArray();

            var count = entities.Length;
            var (size, _) = VRageUtils.GetBound(entities.Select(e => e.GetPosition()));
            return $"{index}: {count} entities in {size / 1000:0.0}km";
        }

        void RespondResult<T>(BaseProfilerResult<T> result, Func<T, int, string> toName)
        {
            Log.Info("Got result from profiling via command");

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Finished profiling; past {result.TotalTime:0.00}ms ({result.TotalTime / 1000:0.00}s) and {result.TotalFrameCount} frames");

            foreach (var ((item, profilerEntry), index) in result.GetTopEntities(_args.Top).Select((v, i) => (v, i)))
            {
                var totalTime = $"{profilerEntry.TotalTime:0.00}ms";
                var mainThreadTime = $"{profilerEntry.MainThreadTime / result.TotalFrameCount:0.00}ms/f";
                var name = toName(item, index);
                messageBuilder.AppendLine($"{name}: {mainThreadTime} (total {totalTime})");
            }

            Log.Info("Finished showing profiler result via command");
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