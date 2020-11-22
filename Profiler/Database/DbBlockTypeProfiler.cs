using System;
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
                using (ProfilerResultQueue.Instance.Profile(profiler))
                {
                    profiler.MarkStart();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<Type> result)
        {
            foreach (var (type, entry) in result.GetTopEntities(MaxDisplayCount))
            {
                InfluxDbPointFactory
                    .Measurement("profiler_block_types")
                    .Tag("block_type", type.Name)
                    .Field("main_ms", (float) entry.TotalMainThreadTime / result.TotalFrameCount)
                    .Write();
            }
        }
    }
}