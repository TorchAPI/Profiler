using System;
using System.Collections.Generic;
using Havok;
using Profiler.Core;
using Profiler.Core.Patches;

namespace Profiler.Basics
{
    public sealed class PhysicsProfiler : BaseProfiler<HkWorld>
    {
        readonly DateTime _dateTime;

        public PhysicsProfiler()
        {
            _dateTime = DateTime.Now;
        }

        public override void MarkStart()
        {
            base.MarkStart();

            MyPhysics_StepWorlds.FlagContinuous(this);
        }

        public override void MarkEnd()
        {
            base.MarkEnd();
            MyPhysics_StepWorlds.UnflagContinuous(this);
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<HkWorld> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.Physics) return;
            if (profilerResult.GameEntity is not HkWorld world) return;
            acceptedKeys.Add(world);
        }

        public override void Dispose()
        {
            MarkEnd();
            base.Dispose();
        }

        public override string ToString() // for MyPhysics_StepWorlds.Flags.ToString
        {
            return $"{nameof(PhysicsProfiler)}({_dateTime:yyyy/MM/dd HH:mm:ss})";
        }
    }
}