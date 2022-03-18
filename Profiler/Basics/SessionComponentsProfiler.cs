using System.Collections.Generic;
using NLog;
using Profiler.Core;
using VRage.Game.Components;

namespace Profiler.Basics
{
    public sealed class SessionComponentsProfiler : BaseProfiler<MySessionComponentBase>
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        protected override void Accept(in ProfilerResult profilerResult, ICollection<MySessionComponentBase> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.UpdateSessionComponents) return;
            var key = profilerResult.GameEntity as MySessionComponentBase;
            acceptedKeys.Add(key);
            //Log.Trace($"accepted: {profilerResult}, {key}");
        }
    }
}