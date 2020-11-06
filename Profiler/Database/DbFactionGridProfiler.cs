using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using TorchDatabaseIntegration.InfluxDB;

namespace Profiler.Database
{
    public sealed class DbFactionGridProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        readonly DbProfilerConfig _config;

        public DbFactionGridProfiler(DbProfilerConfig config)
        {
            _config = config;
        }

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                // make a faction mask
                var faction = MySession.Static.Factions.TryGetFactionByTag(_config.FactionTag);
                var factionId = faction?.FactionId;
                var gameEntityMask = new GameEntityMask(null, null, factionId);

                using (var profiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(profiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var gridProfilerEntries = profiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, gridProfilerEntries);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
        {
            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .ToArray();

            foreach (var (grid, profilerEntry) in topResults)
            {
                if (grid.Closed) continue; // deleted by now

                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                InfluxDbPointFactory
                    .Measurement("profiler_faction_grids")
                    .Tag("faction_tag", _config.FactionTag)
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", deltaTime)
                    .Write();
            }
        }
    }
}