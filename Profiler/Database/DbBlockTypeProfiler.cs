using System;
using System.Linq;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using InfluxDb;

namespace Profiler.Database
{
    public sealed class DbBlockTypeProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var profiler = new BlockTypeProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(profiler))
                {
                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<Type> result)
        {
            var entities = result.GetTopAverageTotalTimesWithRemainder(k => k.Name);
            foreach (var (name, timeMs) in entities.Take(MaxDisplayCount))
            {
                InfluxDbPointFactory
                    .Measurement("profiler_block_types")
                    .Tag("block_type", name)
                    .Field("main_ms", timeMs)
                    .Write();
            }
        }
    }
}