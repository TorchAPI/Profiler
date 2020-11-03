using Profiler.Core;
using Sandbox.Game.World;

namespace Profiler.Basics
{
    public sealed class PlayerProfiler : BaseProfiler<MyIdentity>
    {
        readonly GameEntityMask _mask;

        public PlayerProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(ProfilerResult profilerResult, out MyIdentity key)
        {
            key = null;
            if (profilerResult.Entrypoint != ProfilerPatch.GeneralEntrypoint) return false;

            var playerIdOrNull = _mask.ExtractPlayer(profilerResult.GameEntity);
            if (!(playerIdOrNull is long playerId)) return false;

            var identity = MySession.Static.Players.TryGetIdentity(playerId);
            if (identity == null) return false;

            key = identity;
            return true;
        }
    }
}