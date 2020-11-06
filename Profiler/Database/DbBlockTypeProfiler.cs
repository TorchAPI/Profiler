using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using TorchDatabaseIntegration.InfluxDB;

namespace Profiler.Database
{
    public sealed class DbBlockTypeProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 4;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var profiler = new BlockTypeProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var blockTypeProfilerEntities = profiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, blockTypeProfilerEntities);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(Type Type, ProfilerEntry ProfilerEntry)> entities)
        {
            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (blockType, profilerEntry) in topResults)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                InfluxDbPointFactory
                    .Measurement("profiler_block_types")
                    .Tag("block_type", blockType.Name)
                    .Field("main_ms", deltaTime)
                    .Write();
            }
        }
    }
}