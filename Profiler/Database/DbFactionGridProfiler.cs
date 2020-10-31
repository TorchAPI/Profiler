using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using InfluxDB.Client.Writes;
using Profiler.Basics;
using Profiler.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Torch.Server.InfluxDb;

namespace Profiler.Database
{
    public sealed class DbFactionGridProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;
        readonly InfluxDbClient _dbClient;
        readonly DbProfilerConfig _config;

        public DbFactionGridProfiler(
            InfluxDbClient dbClient,
            DbProfilerConfig config)
        {
            _dbClient = dbClient;
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

                using (var gridProfiler = new GridProfiler(gameEntityMask))
                using (ProfilerPatch.Profile(gridProfiler))
                {
                    var startTick = ProfilerPatch.CurrentTick;

                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var totalTicks = ProfilerPatch.CurrentTick - startTick;

                    var gridProfilerEntries = gridProfiler.GetProfilerEntries();
                    OnProfilingFinished(totalTicks, gridProfilerEntries);
                }
            }
        }

        void OnProfilingFinished(ulong totalTicks, IEnumerable<(MyCubeGrid Grid, ProfilerEntry ProfilerEntry)> entities)
        {
            var points = new List<PointData>();

            var topResults = entities
                .OrderByDescending(r => r.ProfilerEntry.TotalTimeMs)
                .ToArray();

            foreach (var (grid, profilerEntry) in topResults)
            {
                if (grid.Closed) continue; // deleted by now

                var deltaTime = (float) profilerEntry.TotalTimeMs / totalTicks;

                var point = _dbClient.MakePointIn("profiler_faction_grids")
                    .Tag("faction_tag", _config.FactionTag)
                    .Tag("grid_name", grid.DisplayName)
                    .Field("main_ms", deltaTime);

                points.Add(point);
            }

            _dbClient.WritePoints(points.ToArray());
        }
    }
}