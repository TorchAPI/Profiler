using System;
using Profiler.Util;
using Torch.Managers.PatchManager;
using VRage.Game.Components;

namespace Profiler.Core.Patches
{
    public static class MySessionComponentBase_UpdateBeforeSimulation
    {
        const string Category = ProfilerCategory.UpdateSessionComponents;
        const string Method = nameof(MySessionComponentBase.UpdateBeforeSimulation);
        static readonly Type SelfType = typeof(MySessionComponentBase_UpdateBeforeSimulation);
        static readonly int MethodIndex = MethodIndexer.Instance.GetOrCreateIndexOf(MySessionComponentBase_.Type, Method);

        public static void Patch(PatchContext ctx)
        {
            var prefix = SelfType.StaticMethod(nameof(Prefix));
            var suffix = SelfType.StaticMethod(nameof(Suffix));

            foreach (var method in MySessionComponentBase_.DerivedInstanceMethods(Method))
            {
                ctx.GetPattern(method).Prefixes.Add(prefix);
                ctx.GetPattern(method).Suffixes.Add(suffix);
            }
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