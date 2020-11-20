using System;
using System.Reflection;
using NLog;
using Profiler.TorchUtils;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public sealed class MyTransportLayer_Tick
    {
        const string Category = ProfilerCategory.UpdateNetwork;
        static readonly Type SelfType = typeof(MyTransportLayer_Tick);
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type Type = ReflectionUtils.GetTypeByName("Sandbox.Engine.Multiplayer.MyTransportLayer");
        static readonly MethodInfo Method = Type.InstanceMethod("Tick");
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{Type.FullName}#{Method.Name}");

        public static void Patch(PatchContext ctx)
        {
            Log.Info($"type: {Type.Assembly}");

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