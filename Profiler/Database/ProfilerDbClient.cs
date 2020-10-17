using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client.Writes;
using NLog;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Torch.Server.InfluxDb;
using VRage.Game.ModAPI;

namespace Profiler.Database
{
    public sealed class ProfilerDbClient
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 4;

        static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        readonly InfluxDbClient _dbClient;
        CancellationTokenSource _cancellationTokenSource;

        public ProfilerDbClient(InfluxDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task StartProfiling()
        {
            if (_cancellationTokenSource != null)
            {
                _logger.Warn("already running");
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;

            while (!cancellationToken.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                var totalProfiler = new TotalProfiler();
                using (ProfilerPatch.AddObserverUntilDisposed(totalProfiler))
                using (var gridProfiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(gridProfiler))
                using (var blockTypeProfiler = new BlockTypeProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(blockTypeProfiler))
                using (var factionProfiler = new FactionProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(factionProfiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    _logger.Trace("Profiler round started");

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(SamplingSeconds), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // pass
                    }

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    OnTotalProfilingFinished(totalTicks, totalProfiler.MainThreadMs, totalProfiler.OffThreadMs);

                    var gridProfilerEntries = gridProfiler.GetProfilerEntries();
                    OnGridProfilingFinished(totalTicks, gridProfilerEntries);

                    var blockTypeProfilerEntities = blockTypeProfiler.GetProfilerEntries();
                    OnBlockTypeProfilingFinished(totalTicks, blockTypeProfilerEntities);

                    var factionProfilerEntities = factionProfiler.GetProfilerEntries();
                    OnFactionProfilingFinished(totalTicks, factionProfilerEntities);
                }

                _logger.Trace("Profiler round ended");
            }
        }

        void OnTotalProfilingFinished(ulong totalTicks, long totalMainThreadMs, long totalOffThreadMs)
        {
            var point = _dbClient.MakePointIn("profiler_total")
                .Field("main_thread", (float) totalMainThreadMs / totalTicks)
                .Field("off_thread", (float) totalOffThreadMs / totalTicks);

            _dbClient.WritePoints(point);
        }

        void OnGridProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (grid, profilerEntry) in topResults)
            {
                if (grid.Closed) continue; // deleted by now

                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler")
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", deltaTime);

                points.Add(point);
            }

            _dbClient.WritePoints(points.ToArray());
        }

        void OnBlockTypeProfilingFinished(ulong totalTicks, IEnumerable<(Type Type, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (blockType, profilerEntry) in topResults)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler_block_types")
                    .Tag("block_type", blockType.Name)
                    .Field("main_ms", deltaTime);

                points.Add(point);
            }

            _dbClient.WritePoints(points.ToArray());
        }

        void OnFactionProfilingFinished(ulong totalTicks, IEnumerable<(IMyFaction Faction, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (faction, profilerEntry) in topResults)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler_factions")
                    .Tag("faction_name", faction.Tag)
                    .Field("main_ms", deltaTime);

                points.Add(point);
            }

            _dbClient.WritePoints(points.ToArray());
        }

        public void StopProfiling()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}