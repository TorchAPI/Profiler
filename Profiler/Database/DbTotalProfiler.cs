using System;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using TorchDatabaseIntegration.InfluxDB;

namespace Profiler.Database
{
    public sealed class DbTotalProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var profiler = new TotalProfiler();
                using (ProfilerPatch.Profile(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    OnProfilingFinished(totalTicks, profiler.MainThreadMs, profiler.OffThreadMs);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, long totalMainThreadMs, long totalOffThreadMs)
        {
            InfluxDbPointFactory
                .Measurement("profiler_total")
                .Field("main_thread", (float) totalMainThreadMs / totalTicks)
                .Field("off_thread", (float) totalOffThreadMs / totalTicks)
                .Write();
        }
    }
}