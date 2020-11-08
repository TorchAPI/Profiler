using System;
using System.Collections.Generic;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using TorchDatabaseIntegration.InfluxDB;

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
                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var methodNameEntities = profiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, methodNameEntities);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(string Key, ProfilerEntry ProfilerEntry)> entities)
        {
            foreach (var (methodName, profilerEntry) in entities)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                InfluxDbPointFactory
                    .Measurement("profiler_method_names")
                    .Tag("method_name", methodName)
                    .Field("ms", deltaTime)
                    .Write();
            }
        }
    }
}