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

namespace Profiler.Database
{
    public sealed class ProfilerDbClient
    {
        const int SamplingSeconds = 10;
        const int MaxGridCount = 4;
        const int MaxBlockTypeCount = 4;

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
                using (var gridProfiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(gridProfiler))
                using (var blockTypeProfiler = new BlockTypeProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(blockTypeProfiler))
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

                    var gridProfilerEntries = gridProfiler.GetProfilerEntries();
                    OnGridProfilingFinished(totalTicks, gridProfilerEntries);

                    var blockTypeProfilerEntities = blockTypeProfiler.GetProfilerEntries();
                    OnBlockTypeProfilingFinished(totalTicks, blockTypeProfilerEntities);
                }

                _logger.Trace("Profiler round ended");
            }
        }

        void OnGridProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxGridCount)
                .ToArray();

            foreach (var (grid, profilerEntry) in topResults)
            {
                if (grid.Closed) continue; // deleted by now

                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler")
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", deltaTime);

                points.Add(point);

                _logger.Trace($"point added: '{grid.DisplayName}' {deltaTime:0.0000}ms/f");
            }

            _dbClient.WritePoints(points.ToArray());

            _logger.Trace($"Finished profiling & sending to DB; count: {topResults.Length}");
        }

        void OnBlockTypeProfilingFinished(ulong totalTicks, IEnumerable<(Type Type, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxBlockTypeCount)
                .ToArray();

            foreach (var (blockType, profilerEntry) in topResults)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler_block_types")
                    .Tag("block_type", blockType.Name)
                    .Field("main_ms", deltaTime);

                points.Add(point);

                _logger.Trace($"point added: '{blockType.Name}' {deltaTime:0.0000}ms/f");
            }

            _dbClient.WritePoints(points.ToArray());

            _logger.Trace($"Finished profiling & sending to DB; count: {topResults.Length}");
        }

        public void StopProfiling()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
        }
    }
}