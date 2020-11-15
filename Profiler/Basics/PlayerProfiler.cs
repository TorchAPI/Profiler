using Profiler.Core;
using Sandbox.Game.World;
using VRage.ModAPI;

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
            if (profilerResult.Category != ProfilerCategory.General) return false;

            var playerIdOrNull = _mask.ExtractPlayer(profilerResult.GameEntity as IMyEntity);
            if (!(playerIdOrNull is long playerId)) return false;

            var identity = MySession.Static.Players.TryGetIdentity(playerId);
            if (identity == null) return false;

            key = identity;
            return true;
        }
    }
}