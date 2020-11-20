using System;
using System.Reflection;
using Profiler.TorchUtils;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public sealed class MyDedicatedServer_ReportReplicatedObjects
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateReplication;
        static readonly Type SelfType = typeof(MyDedicatedServer_ReportReplicatedObjects);
        static readonly Type Type = typeof(MyDedicatedServer);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MyDedicatedServer.ReportReplicatedObjects));
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