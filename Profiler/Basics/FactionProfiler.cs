using System;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class FactionProfiler : BaseProfiler<IMyFaction>
    {
        readonly GameEntityMask _mask;

        public FactionProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out IMyFaction key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            if (profilerResult.GameEntity is not IMyEntity entity) return false;
            if (entity.GetParentEntityOfType<MyCubeBlock>() is not { } block) return false;
            if (!_mask.TestBlock(block)) return false;

            key = MySession.Static.Factions.TryGetPlayerFaction(block.OwnerId);
            return key != null;
        }
    }
}