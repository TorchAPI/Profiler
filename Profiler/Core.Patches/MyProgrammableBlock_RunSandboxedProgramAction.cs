using System;
using System.Reflection;
using Profiler.TorchUtils;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public sealed class MyProgrammableBlock_RunSandboxedProgramAction
    {
        const string Category = ProfilerCategory.Scripts;
        static readonly Type SelfType = typeof(MyProgrammableBlock_RunSandboxedProgramAction);
        static readonly Type Type = typeof(MyProgrammableBlock);
        static readonly MethodInfo Method = Type.InstanceMethod("RunSandboxedProgramAction");
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