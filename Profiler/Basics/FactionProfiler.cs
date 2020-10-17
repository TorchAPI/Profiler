using System;
using Profiler.Core;
using Sandbox.Game.World;
using VRage.Game.ModAPI;

namespace Profiler.Basics
{
    public sealed class FactionProfiler : BaseProfiler<IMyFaction>
    {
        readonly GameEntityMask _mask;

        public FactionProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(ProfilerResult profilerResult, out IMyFaction key)
        {
            key = null;

            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return false;

            var player = _mask.ExtractPlayer(profilerResult.GameEntity);
            if (!player.HasValue) return false;

            var faction = MySession.Static.Factions.TryGetPlayerFaction(player.Value);
            if (faction == null) return false;

            key = faction;
            return true;
        }
    }
}