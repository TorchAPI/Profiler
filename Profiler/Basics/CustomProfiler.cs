using System.Collections.Generic;
using NLog;
using Profiler.Core;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class CustomProfiler : BaseProfiler<string>
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly GameEntityMask _mask;
        readonly string _prefix;

        public CustomProfiler(GameEntityMask mask, string prefix)
        {
            _mask = mask;
            _prefix = prefix;
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<string> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.Custom) return;

            var methodName = profilerResult.MethodName;
            if (methodName.StartsWith(_prefix))
            {
                if (profilerResult.GameEntity is IMyEntity entity &&
                    !_mask.TestAll(entity))
                {
                    return;
                }

                acceptedKeys.Add(methodName);
            }
        }
    }
}