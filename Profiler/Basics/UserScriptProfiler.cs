using System;
using System.Collections.Generic;
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

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MyProgrammableBlock> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.Scripts) return;
            if (profilerResult.GameEntity is not MyProgrammableBlock programmableBlock) return; // shouldn't happen
            if (programmableBlock.Closed) return;
            if (!_mask.TestAll(programmableBlock)) return;

            acceptedKeys.Add(programmableBlock);
        }
    }
}