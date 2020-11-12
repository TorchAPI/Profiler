using System;
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
                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<MyCubeGrid> result)
        {
            var results = result.GetTopAverageTotalTimesWithRemainder(k => k.DisplayName);
            foreach (var (name, timeMs) in results.Take(MaxDisplayCount))
            {
                InfluxDbPointFactory
                    .Measurement("profiler")
                    .Tag("grid_name", name)
                    .Field("main_ms", timeMs)
                    .Write();
            }
        }
    }
}