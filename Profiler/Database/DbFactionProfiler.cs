using System;
using System.Collections.Generic;
using System.Threading;
using Profiler.Basics;
using Profiler.Core;
using InfluxDb;
using Sandbox.Game.World;
using TorchUtils;
using VRage.Game.ModAPI;

namespace Profiler.Database
{
    public sealed class DbFactionProfiler : IDbProfiler
    {
        const int SamplingSeconds = 10;

        public void StartProfiling(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                var gameEntityMask = new GameEntityMask(null, null, null);
                using (var profiler = new FactionProfiler(gameEntityMask))
                using (ProfilerResultQueue.Profile(profiler))
                {
                    profiler.MarkStart();
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(SamplingSeconds));

                    var result = profiler.GetResult();
                    OnProfilingFinished(result);
                }
            }
        }

        void OnProfilingFinished(BaseProfilerResult<IMyFaction> result)
        {
            // get online players per faction
            var onlineFactions = new Dictionary<string, int>();
            var onlinePlayers = MySession.Static.Players.GetOnlinePlayers();
            foreach (var onlinePlayer in onlinePlayers)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(onlinePlayer.PlayerId());
                if (faction == null) continue;

                onlineFactions.Increment(faction.Tag);
            }

            foreach (var (faction, entity) in result.GetTopEntities())
            {
                onlineFactions.TryGetValue(faction.Tag, out var onlinePlayerCount);
                onlinePlayerCount = Math.Max(1, onlinePlayerCount); // fix zero division
                var mainMs = entity.MainThreadTime / result.TotalFrameCount;
                var mainMsPerMember = mainMs / onlinePlayerCount;

                InfluxDbPointFactory
                    .Measurement("profiler_factions")
                    .Tag("faction_tag", faction.Tag)
                    .Field("main_ms", mainMs)
                    .Field("main_ms_per_member", mainMsPerMember)
                    .Write();
            }
        }
    }
}