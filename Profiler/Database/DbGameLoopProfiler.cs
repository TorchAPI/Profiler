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
                using (ProfilerResultQueue.Instance.Profile(profiler))
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
            var frameMs = (float) result.TotalTime.TotalMilliseconds;
            var updateMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.Update, 0);
            var waitMs = frameMs - updateMs;
            var updateNetworkMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateNetwork, 0);
            var updateReplMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateReplication, 0);
            var updateSessionCompsMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateSessionComponents, 0);
            var updateSessionCompsAllMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateSessionComponentsAll, 0);
            var updateSessionCompsOtherMs = updateSessionCompsAllMs - updateSessionCompsMs;
            var updateGpsMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateGps, 0);
            var updateParallelWaitMs = (float) result.GetMainThreadTickMsOrElse(ProfilerCategory.UpdateParallelWait, 0);
            var updateOtherMs = updateMs - updateNetworkMs - updateReplMs - updateSessionCompsAllMs - updateGpsMs - updateParallelWaitMs;

            InfluxDbPointFactory
                .Measurement("profiler_game_loop")
                .Field("tick", result.TotalFrameCount)
                .Field("frame", frameMs / result.TotalFrameCount)
                .Field("wait", waitMs / result.TotalFrameCount)
                .Field("update", updateMs / result.TotalFrameCount)
                .Field("update_network", updateNetworkMs / result.TotalFrameCount)
                .Field("update_replication", updateReplMs / result.TotalFrameCount)
                .Field("update_session_components", updateSessionCompsMs / result.TotalFrameCount)
                .Field("update_session_components_all", updateSessionCompsAllMs / result.TotalFrameCount)
                .Field("update_session_components_other", updateSessionCompsOtherMs / result.TotalFrameCount)
                .Field("update_gps", updateGpsMs / result.TotalFrameCount)
                .Field("update_parallel_wait", updateParallelWaitMs / result.TotalFrameCount)
                .Field("update_other", updateOtherMs / result.TotalFrameCount)
                .Write();
        }
    }
}