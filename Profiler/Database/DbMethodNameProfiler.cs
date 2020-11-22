using System;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using InfluxDb;

namespace Profiler.Database
{
    public sealed class DbMethodNameProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                using (var profiler = new MethodNameProfiler())
                using (ProfilerResultQueue.Profile(profiler))
                {
                    profiler.MarkStart();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<string> result)
        {
            foreach (var (name, entity) in result.GetTopEntities())
            {
                InfluxDbPointFactory
                    .Measurement("profiler_method_names")
                    .Tag("method_name", name)
                    .Field("ms", (float) entity.MainThreadTime / result.TotalFrameCount)
                    .Write();
            }
        }
    }
}