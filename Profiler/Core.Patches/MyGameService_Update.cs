using System;
using System.Reflection;
using Profiler.TorchUtils;
using Sandbox.Engine.Networking;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public static class MyGameService_Update
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateNetwork;
        static readonly Type SelfType = typeof(MyGameService_Update);
        static readonly MethodInfo Method = typeof(MyGameService).StaticMethod(nameof(MyGameService.Update));
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{typeof(MyGameService).FullName}#{nameof(MyGameService.Update)}");

        public static void Patch(PatchContext ctx)
        {
            var prefix = SelfType.StaticMethod(nameof(Prefix));
            var suffix = SelfType.StaticMethod(nameof(Suffix));

            ctx.GetPattern(Method).Prefixes.Add(prefix);
            ctx.GetPattern(Method).Suffixes.Add(suffix);
        }

        // ReSharper disable once RedundantAssignment
        // ReSharper disable once UnusedParameter.Local
        static void Prefix(ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = new ProfilerToken(null, MethodIndex, Category);
        }

        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}