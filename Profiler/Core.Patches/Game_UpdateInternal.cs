using System;
using System.Reflection;
using NLog;
using Profiler.Util;
using Sandbox.Engine.Platform;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    internal static class Game_UpdateInternal
    {
        public const string Category = "Total";

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly MethodInfo Method = typeof(Game).InstanceMethod("UpdateInternal");
        static readonly int MethodIndex = MethodIndexer.Instance.GetOrCreateIndexOf($"{typeof(Game).FullName}#UpdateInternal");

        public static void Patch(PatchContext ctx)
        {
            var prefix = typeof(Game_UpdateInternal).StaticMethod(nameof(Prefix));
            var suffix = typeof(Game_UpdateInternal).StaticMethod(nameof(Suffix));

            ctx.GetPattern(Method).Prefixes.Add(prefix);
            ctx.GetPattern(Method).Suffixes.Add(suffix);
        }

        // ReSharper disable once RedundantAssignment
        // ReSharper disable once UnusedParameter.Local
        static void Prefix(Game __instance, ref ProfilerToken? __localProfilerHandle)
        {
            //Log.Info("updateinternal");
            __localProfilerHandle = new ProfilerToken(null, MethodIndex, Category, DateTime.UtcNow);
        }

        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}