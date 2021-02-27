using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public static class MyGameService_Update
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateNetwork;
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MyGameService_Update);
        static readonly MethodInfo Method = typeof(MyGameService).GetStaticMethod(nameof(MyGameService.Update));
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{typeof(MyGameService).FullName}#{nameof(MyGameService.Update)}");

        public static void Patch(PatchContext ctx)
        {
            try
            {
                var prefix = SelfType.GetStaticMethod(nameof(Prefix));
                var suffix = SelfType.GetStaticMethod(nameof(Suffix));

                ctx.GetPattern(Method).Prefixes.Add(prefix);
                ctx.GetPattern(Method).Suffixes.Add(suffix);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to patch: {e.Message}");
            }
        }

        // ReSharper disable once RedundantAssignment
        // ReSharper disable once UnusedParameter.Local
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Prefix(ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = ProfilerPatch.StartToken(null, MethodIndex, Category);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle);
        }
    }
}