using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using InfluxDB.Client.Writes;
using NLog;
using Profiler.Core;
using Profiler.Interactive;
using Sandbox.Game.Entities;
using Torch.Server.InfluxDb;

namespace Profiler.Database
{
    public sealed class ProfilerDbClient
    {
        const int SamplingSeconds = 10;
        const int MaxGridCount = 10;

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
                    var profilerEntries = gridProfiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, profilerEntries);
                }

                _logger.Trace("Profiler round ended");
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(long GridID, ProfilerEntry ProfilerEntry)> results)
        {
            var points = new List<PointData>();

            var topResults = results
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxGridCount)
                .ToArray();

            foreach (var (gridId, profilerEntry) in topResults)
            {
                if (!MyEntities.TryGetEntityById(gridId, out var entry)) continue; // deleted by now
                var grid = (MyCubeGrid) entry;

                var gridName = grid.DisplayName;
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler")
                    .Tag("grid_name", gridName)
                    .Field("main_ms", deltaTime);

                points.Add(point);

                _logger.Trace($"point added: '{gridName}' {deltaTime:0.00}");
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