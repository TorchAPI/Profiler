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
                using (ProfilerPatch.Profile(profiler))
                {
                    profiler.StartProcessQueue();
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

            var results = result.GetTopAverageTotalTimesWithRemainder(k => k.Tag);
            foreach (var (name, timeMs) in results)
            {
                onlineFactions.TryGetValue(name, out var onlinePlayerCount);

                InfluxDbPointFactory
                    .Measurement("profiler_factions")
                    .Tag("faction_tag", name)
                    .Field("main_ms", timeMs)
                    .Field("online_player_count", onlinePlayerCount)
                    .Write();
            }
        }
    }
}