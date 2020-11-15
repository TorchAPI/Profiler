using System;
using System.Reflection;
using Profiler.Util;
using Torch.Managers.PatchManager;
using VRage.Game.Components;

namespace Profiler.Core.Patches
{
    public sealed class MySessionComponentBase_Simulate
    {
        const string Category = ProfilerCategory.UpdateSessionComponents;
        static readonly Type SelfType = typeof(MySessionComponentBase_Simulate);
        static readonly Type Type = typeof(MySessionComponentBase);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MySessionComponentBase.Simulate));
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
            __localProfilerHandle = new ProfilerToken(__instance, MethodIndex, Category, DateTime.UtcNow);
        }

        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}