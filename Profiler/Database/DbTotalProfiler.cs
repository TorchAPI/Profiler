using System;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using InfluxDb;

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
                    profiler.Start();

                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    profiler.Stop();

                    OnProfilingFinished(profiler.Ticks, profiler.TimeMs, profiler.GameTimeMs);
                }
            }
        }

        void OnProfilingFinished(ulong ticks, double timeMs, double gameTimeMs)
        {
            InfluxDbPointFactory
                .Measurement("profiler_total")
                .Field("tick", ticks)
                .Field("total", (float) timeMs / ticks)
                .Field("game", (float) gameTimeMs / ticks)
                .Field("wait", (float) (timeMs - gameTimeMs) / ticks)
                .Write();
        }
    }
}