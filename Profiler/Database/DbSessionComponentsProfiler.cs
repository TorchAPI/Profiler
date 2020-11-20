using System;
using System.Threading;
using InfluxDb;
using Profiler.Basics;
using Profiler.Core;
using VRage.Game.Components;

namespace Profiler.Database
{
    public sealed class DbSessionComponentsProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                using (var profiler = new SessionComponentsProfiler())
                using (ProfilerResultQueue.Instance.Profile(profiler))
                {
                    profiler.MarkStart();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<MySessionComponentBase> result)
        {
            foreach (var (comp, entity) in result.GetTopEntities())
            {
                InfluxDbPointFactory
                    .Measurement("profiler_game_loop_session_components")
                    .Tag("comp_name", comp.GetType().Name)
                    .Field("main_ms", (float) entity.TotalMainThreadTime / result.TotalFrameCount)
                    .Write();
            }
        }
    }
}