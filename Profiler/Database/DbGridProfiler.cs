using System;
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
                using (ProfilerResultQueue.Profile(profiler))
                {
                    profiler.MarkStart();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<MyCubeGrid> result)
        {
            foreach (var (grid, entity) in result.GetTopEntities(MaxDisplayCount))
            {
                InfluxDbPointFactory
                    .Measurement("profiler")
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", (float) entity.TotalMainThreadTime / result.TotalFrameCount)
                    .Write();
            }
        }
    }
}