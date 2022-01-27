using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
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

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyIdentity key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;

            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;

            key = MySession.Static.Players.TryGetIdentity(block.OwnerId);
            return key != null;
        }
    }
}