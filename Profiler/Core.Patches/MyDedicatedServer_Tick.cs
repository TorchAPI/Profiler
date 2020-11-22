using System;
using System.Reflection;
using NLog;
using Profiler.TorchUtils;
using Sandbox.Engine.Multiplayer;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public static class MyDedicatedServer_Tick
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateNetwork;
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MyDedicatedServer_Tick);
        static readonly Type Type = typeof(MyDedicatedServer);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MyDedicatedServer.Tick));
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{Type.FullName}#{Method.Name}");

        public static void Patch(PatchContext ctx)
        {
            try
            {
                var prefix = SelfType.StaticMethod(nameof(Prefix));
                var suffix = SelfType.StaticMethod(nameof(Suffix));

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