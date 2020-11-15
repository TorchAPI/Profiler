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
            var updateSessionCompsMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateSessionComponents, 0);
            var updateSessionCompsAllMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateSessionComponentsAll, 0);
            var updateSessionCompsOtherMs = updateSessionCompsAllMs - updateSessionCompsMs;
            var updateGpsMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateGps, 0);
            var updateParallelWaitMs = (float) result.GetMainThreadMsOrElse(ProfilerCategory.UpdateParallelWait, 0);
            var updateOtherMs = updateMs - updateNetworkMs - updateReplMs - updateSessionCompsAllMs - updateGpsMs - updateParallelWaitMs;

            InfluxDbPointFactory
                .Measurement("profiler_game_loop")
                .Field("tick", result.TotalTicks)
                .Field("frame", frameMs / result.TotalTicks)
                .Field("wait", waitMs / result.TotalTicks)
                .Field("update", updateMs / result.TotalTicks)
                .Field("update_network", updateNetworkMs / result.TotalTicks)
                .Field("update_replication", updateReplMs / result.TotalTicks)
                .Field("update_session_components", updateSessionCompsMs / result.TotalTicks)
                .Field("update_session_components_all", updateSessionCompsAllMs / result.TotalTicks)
                .Field("update_session_components_other", updateSessionCompsOtherMs / result.TotalTicks)
                .Field("update_gps", updateGpsMs / result.TotalTicks)
                .Field("update_parallel_wait", updateParallelWaitMs / result.TotalTicks)
                .Field("update_other", updateOtherMs / result.TotalTicks)
                .Write();
        }
    }
}