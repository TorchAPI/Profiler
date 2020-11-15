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
                using (ProfilerPatch.Profile(profiler))
                {
                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<string> result)
        {
            foreach (var (name, entity) in result.GetTop())
            {
                InfluxDbPointFactory
                    .Measurement("profiler_method_names")
                    .Tag("method_name", name)
                    .Field("ms", (float) entity.TotalMainThreadTimeMs / result.TotalTicks)
                    .Write();
            }
        }
    }
}