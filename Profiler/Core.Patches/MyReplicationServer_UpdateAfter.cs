using System;
using System.Reflection;
using Profiler.Util;
using Torch.Managers.PatchManager;
using VRage.Network;

namespace Profiler.Core.Patches
{
    public static class MyReplicationServer_UpdateAfter
    {
        const string Category = ProfilerCategory.UpdateReplication;
        static readonly Type SelfType = typeof(MyReplicationServer_UpdateAfter);
        static readonly Type Type = typeof(MyReplicationServer);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MyReplicationServer.UpdateAfter));
        static readonly int MethodIndex = MethodIndexer.Instance.GetOrCreateIndexOf($"{Type.FullName}#{Method.Name}");

        public static void Patch(PatchContext ctx)
        {
            var prefix = SelfType.StaticMethod(nameof(Prefix));
            var suffix = SelfType.StaticMethod(nameof(Suffix));

            ctx.GetPattern(Method).Prefixes.Add(prefix);
            ctx.GetPattern(Method).Suffixes.Add(suffix);
        }

        // ReSharper disable once RedundantAssignment
        // ReSharper disable once UnusedParameter.Local
        static void Prefix(object __instance, ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = new ProfilerToken(null, MethodIndex, Category, DateTime.UtcNow);
        }

        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}