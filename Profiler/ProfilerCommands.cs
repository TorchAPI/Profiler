using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Havok;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Profiler.Interactive;
using Profiler.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Profiler
{
    [Category("profile")]
    public sealed class ProfilerCommands : CommandModule
    {
        const string HelpText = "type !profile help";
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

        [Command("sim", "Check simspeed", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void Sim()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                Context.Respond($"Started profiling the sim, result in {_args.Seconds}s");

                var monitor = new SimMonitor((int)_args.Seconds);
                await monitor.Monitor();

                Log.Info("Got result from profiling via command");

                var messageBuilder = new StringBuilder();
                messageBuilder.AppendLine($"Finished profiling; past {_args.Seconds}s");
                messageBuilder.AppendLine($"Best sim: {monitor.Max:0.0}");
                messageBuilder.AppendLine($"Worst sim: {monitor.Min:0.0}");
                messageBuilder.AppendLine($"Average sim: {monitor.Avg:0.0}");
                Context.Respond(messageBuilder.ToString());

                Log.Info("Finished showing profiler result via command");
            });
        }

        [Command("frames", "Profiles game frames", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileFrames()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);

                using (var profiler = new GameLoopProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling frames, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (b, _) => ProfilerCategoryToNameOrNull(b));
                }
            });
        }

        static string ProfilerCategoryToNameOrNull(ProfilerCategory category) => category switch
        {
            ProfilerCategory.Frame => null,
            ProfilerCategory.Lock => "Idle",
            ProfilerCategory.Update => null,
            ProfilerCategory.UpdateNetwork => "Network",
            ProfilerCategory.UpdateReplication => "Replication",
            ProfilerCategory.UpdateSessionComponents => "Session",
            ProfilerCategory.UpdateGps => "GPS",
            ProfilerCategory.UpdateParallelWait => null,
            ProfilerCategory.General => null,
            ProfilerCategory.Scripts => null,
            ProfilerCategory.UpdateNetworkEvent => null,
            ProfilerCategory.UpdateParallelRun => null,
            ProfilerCategory.Physics => null,
            ProfilerCategory.Custom => null,
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
        };

        [Command("blocktypes", "Profiles performance per block type", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileBlockType()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new BlockTypeProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block type, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (b, _) => BlockTypeToString(b));
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
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new BlockDefinitionProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling by block definition, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (k, _) => k.BlockPairName);
                }
            });
        }

        [Command("grids", "Profiles performance per grid", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileGrids()
        {
            BaseProfiler<MyCubeGrid> GetProfiler(GameEntityMask mask)
            {
                if (_args.TryGetValue("block", out var blockMask))
                {
                    return new GridByBlockTypeProfiler(mask, blockMask);
                }

                if (_args.HasFlagValue("noblocks"))
                {
                    return new GridOnlyProfiler(mask);
                }

                return new GridProfiler(mask);
            }

            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);
                using (var profiler = GetProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling grids, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (g, _) => GridToResultText(g));

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
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new FactionProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling factions, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (f, _) => f.Tag);
                }
            });
        }

        [Command("players", "Profiles performance per player", HelpText)]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfilePlayers()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new PlayerProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling players, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (k, _) => k.DisplayName);
                }
            });
        }

        [Command("scripts", "Profiles performance of programmable blocks")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileScripts()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

                using (var profiler = new UserScriptProfiler(mask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling scripts, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (p, _) => PbToString(p));
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
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                using (var profiler = new SessionComponentsProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling sessions, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (p, _) => p.GetType().Name);
                }
            });
        }

        [Command("entities", "Profiles performance of entities by type")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileEntityTypes()
        {
            this.CatchAndReportAsync(async () =>
            {
                _args = new RequestParamParser(Context.Player, Context.Args);
                using (var profiler = new EntityTypeProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    Context.Respond($"Started profiling entity types, result in {_args.Seconds}s");

                    profiler.MarkStart();
                    await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                    profiler.MarkEnd();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (p, _) => p);
                }
            });
        }

        [Command("physics", "Profiles performance of physics clusters")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfilePhysics()
        {
            this.CatchAndReportAsync(async () =>
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

                    msg.Append("(end of list)");
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

                    await VRageUtils.MoveToGameLoop();

                    profiler.MarkStart();

                    for (var _ = 0; _ < physicsParams.Tics; _++)
                    {
                        await VRageUtils.MoveToGameLoop();
                    }

                    profiler.MarkEnd();

                    await TaskUtils.MoveToThreadPool();

                    var result = profiler.GetResult();
                    RespondResult(result, false, (w, i) => GetWorldName(w, i, mask));

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
                .WhereAssignable<IMyEntity, MyCubeGrid>()
                .Where(e => mask.TestAll(e))
                .ToArray();

            var count = entities.Length;
            var (size, _) = VRageUtils.GetBound(entities.Select(e => e.PositionComp.GetPosition()));
            return $"{index}: {count} entities in {size / 1000:0.0}km";
        }

        [Command("custom", "Profiles custom measurements. `--prefix=` to specify the path.")]
        [Permission(MyPromoteLevel.Moderator)]
        public void ProfileCustom() => this.CatchAndReportAsync(async () =>
        {
            _args = new RequestParamParser(Context.Player, Context.Args);
            var mask = new GameEntityMask(_args.PlayerMask, _args.GridMask, _args.FactionMask);

            if (!_args.TryGetValue("prefix", out var prefix))
            {
                throw new InvalidOperationException("Must specify prefix by `--prefix=`");
            }

            using (var profiler = new CustomProfiler(mask, prefix))
            using (ProfilerResultQueue.Profile(profiler))
            {
                Context.Respond($"Started profiling custom measurements, result in {_args.Seconds} seconds");
                await VRageUtils.MoveToGameLoop();

                profiler.MarkStart();
                await Task.Delay(TimeSpan.FromSeconds(_args.Seconds));
                profiler.MarkEnd();

                var result = profiler.GetResult();
                RespondResult(result, true, (p, _) => p);
            }
        });

        void RespondResult<T>(BaseProfilerResult<T> result, bool showWorkerThreads, Func<T, int, string> toNameOrNull)
        {
            Log.Info("Got result from profiling via command");

            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"Finished profiling; past {result.TotalTime:0.00}ms ({result.TotalTime / 1000:0.00}s) and {result.TotalFrameCount} frames");

            foreach (var ((item, profilerEntry), index) in result.GetTopEntities(_args.Top).Select((v, i) => (v, i)))
            {
                var totalTime = $"{profilerEntry.TotalTime:0.00}ms";
                var mainThreadTime = $"{profilerEntry.MainThreadTime / result.TotalFrameCount:0.00}ms/f";
                var workerThreadTime = $"{profilerEntry.OffThreadTime / result.TotalFrameCount:0.00}ms/f";
                var name = toNameOrNull(item, index);
                if (string.IsNullOrEmpty(name)) continue;

                messageBuilder.AppendLine(showWorkerThreads
                    ? $"{name}: main: {mainThreadTime}, off: {workerThreadTime}, total: {totalTime}"
                    : $"{name}: {mainThreadTime} (total {totalTime})");
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
            const string url = "https://torchapi.com/wiki/index.php/Plugins/Profiler";

            if (Context.Player?.IdentityId is { } playerId)
            {
                Context.Respond("Opening wiki on the steam overlay");
                var steamOverlayUrl = MakeSteamOverlayUrl(url);
                MyVisualScriptLogicProvider.OpenSteamOverlay(steamOverlayUrl, playerId);
            }
            else if (Context.GetType() == typeof(ConsoleCommandContext))
            {
                Context.Respond("Opening wiki on the default web browser");
                Process.Start(url);
            }
            else
            {
                Context.Respond(url);
            }
        }

        static string MakeSteamOverlayUrl(string baseUrl)
        {
            const string steamOverlayFormat = "https://steamcommunity.com/linkfilter/?url={0}";
            return string.Format(steamOverlayFormat, baseUrl);
        }
    }
}