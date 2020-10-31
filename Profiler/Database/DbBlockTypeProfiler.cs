using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InfluxDB.Client.Writes;
using Profiler.Basics;
using Profiler.Core;
using Torch.Server.InfluxDb;

namespace Profiler.Database
{
    public sealed class DbBlockTypeProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        const int MaxDisplayCount = 4;

        readonly InfluxDbClient _dbClient;

        public DbBlockTypeProfiler(InfluxDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var blockTypeProfiler = new BlockTypeProfiler(gameEntityMask))
                using (ProfilerPatch.AddObserverUntilDisposed(blockTypeProfiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var blockTypeProfilerEntities = blockTypeProfiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, blockTypeProfilerEntities);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(Type Type, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .Take(MaxDisplayCount)
                .ToArray();

            foreach (var (blockType, profilerEntry) in topResults)
            {
                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler_block_types")
                    .Tag("block_type", blockType.Name)
                    .Field("main_ms", deltaTime);

                points.Add(point);
            }

            _dbClient.WritePoints(points.ToArray());
        }
    }
}