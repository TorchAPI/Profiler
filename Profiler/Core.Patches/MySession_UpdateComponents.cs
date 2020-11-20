using System;
using System.Reflection;
using Profiler.TorchUtils;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public sealed class MySession_UpdateComponents
    {
        const string Category = ProfilerCategory.UpdateSessionComponentsAll;
        static readonly Type SelfType = typeof(MySession_UpdateComponents);
        static readonly Type Type = typeof(MySession);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MySession.UpdateComponents));
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{Type.FullName}#{Method.Name}");

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
            __localProfilerHandle = new ProfilerToken(null, MethodIndex, Category);
        }

        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}