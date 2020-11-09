using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using InfluxDb;

namespace Profiler.Database
{
    public sealed class DbGridProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 4;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var profiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var gridProfilerEntries = profiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, gridProfilerEntries);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
        {
            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (grid, profilerEntry) in topResults)
            {
                if (grid.Closed) continue; // deleted by now

                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                InfluxDbPointFactory
                    .Measurement("profiler")
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", deltaTime)
                    .Write();
            }
        }
    }
}