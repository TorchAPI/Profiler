using NLog;
using Profiler.Core;
using Profiler.Utils;
using Sandbox.Game.Entities;
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

        protected override bool TryAccept(in ProfilerResult profilerResult, out string key)
        {
            key = null;
            if (profilerResult.Category != ProfilerCategory.Custom) return false;

            key = profilerResult.MethodName;
            if (!key.StartsWith(_prefix)) return false;

            if (profilerResult.GameEntity is IMyEntity entity &&
                entity.GetParentEntityOfType<MyCubeBlock>() is { } block &&
                !_mask.TestBlock(block))
            {
                return false;
            }

            return true;
        }
    }
}