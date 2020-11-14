using System;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using InfluxDb;

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
                    profiler.StartProcessQueue();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<MyCubeGrid> result)
        {
            foreach (var (grid, entity) in result.GetTopEntities())
            {
                InfluxDbPointFactory
                    .Measurement("profiler_faction_grids")
                    .Tag("faction_tag", _config.FactionTag)
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", (float) entity.TotalMainThreadTimeMs / result.TotalTicks)
                    .Write();
            }
        }
    }
}