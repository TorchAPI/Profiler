using System;
using System.Reflection;
using NLog;
using Profiler.TorchUtils;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public static class MyNetworkReader_Process
    {
        const string Category = ProfilerCategory.UpdateNetwork;
        static readonly Type SelfType = typeof(MyNetworkReader_Process);
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type Type = ReflectionUtils.GetTypeByName("Sandbox.Engine.Networking.MyNetworkReader");
        static readonly MethodInfo Method = Type.StaticMethod("Process");
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{Type.FullName}#{Method.Name}");

        public static void Patch(PatchContext ctx)
        {
            Log.Info($"type: {Type.AssemblyQualifiedName}");

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