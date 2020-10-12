using System;
using System.Collections.Generic;
using System.Linq;
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
        bool _running;

        public ProfilerDbClient(InfluxDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task StartProfiling()
        {
            if (_running)
            {
                _logger.Warn("already running");
                return;
            }

            _running = true;

            _logger.Info("Starting profiler...");

            while (_running)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                var gridProfiler = new GridProfiler(gameEntityMask);
                ProfilerPatch.AddObserver(gridProfiler);
                var startTick = ProfilerPatch.CurrentTick;

                _logger.Trace("Profiler round started");

                await Task.Delay(TimeSpan.FromSeconds(SamplingSeconds));

                var totalTicks = ProfilerPatch.CurrentTick - startTick;
                var profilerEntries = gridProfiler.GetProfilerEntries();
                OnProfilingFinished(totalTicks, profilerEntries);

                ProfilerPatch.RemoveObserver(gridProfiler);
                gridProfiler.Dispose();

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
            _running = false;
        }
    }
}