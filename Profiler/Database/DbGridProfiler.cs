using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InfluxDB.Client.Writes;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Torch.Server.InfluxDb;

namespace Profiler.Database
{
    public sealed class DbGridProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 4;

        readonly InfluxDbClient _dbClient;

        public DbGridProfiler(InfluxDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var gridProfiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(gridProfiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var gridProfilerEntries = gridProfiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, gridProfilerEntries);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
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
    }
}