using System;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using InfluxDb;

namespace Profiler.Database
{
    public sealed class DbGameLoopProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var profiler = new GameLoopProfiler();
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
            var frameMs = (float) result.TotalTime.TotalMilliseconds;
            var updateMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.Update, 0);
            var waitMs = frameMs - updateMs;
            var updateNetworkMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateNetwork, 0);
            var updateReplMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateReplication, 0);
            var updateCompsMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateSessionComponents, 0);
            var updateOtherMs = updateMs - updateNetworkMs - updateReplMs - updateCompsMs;

            InfluxDbPointFactory
                .Measurement("profiler_game_loop")
                .Field("tick", result.TotalTicks)
                .Field("frame", frameMs / result.TotalTicks)
                .Field("wait", waitMs / result.TotalTicks)
                .Field("update", updateMs / result.TotalTicks)
                .Field("update_network", updateNetworkMs / result.TotalTicks)
                .Field("update_replication", updateReplMs / result.TotalTicks)
                .Field("update_components", updateCompsMs / result.TotalTicks)
                .Field("update_other", updateOtherMs / result.TotalTicks)
                .Write();
        }
    }
}