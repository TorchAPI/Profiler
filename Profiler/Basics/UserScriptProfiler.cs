using System;
using Profiler.Core;
using Sandbox.Game.Entities.Blocks;

namespace Profiler.Basics
{
    public sealed class UserScriptProfiler : BaseProfiler<MyProgrammableBlock>
    {
        readonly GameEntityMask _mask;

        public UserScriptProfiler(GameEntityMask mask)
        {
            _mask = mask;
        }

        protected override bool TryAccept(in ProfilerResult profilerResult, out MyProgrammableBlock key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.Scripts) return false;
            if (!(profilerResult.GameEntity is MyProgrammableBlock programmableBlock)) return false; // shouldn't happen
            if (programmableBlock.Closed) return false;
            if (!_mask.TestBlock(programmableBlock)) return false;

            key = programmableBlock;
            return true;
        }
    }
}