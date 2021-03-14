using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Torch.Managers.PatchManager;
using VRage;
using VRage.Network;
using VRageMath;
using CallSite = VRage.Network.CallSite;

namespace Profiler.Core.Patches
{
    public static class MyReplicationServer_OnEvent
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateNetworkEvent;
        const BindingFlags BindingFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MyReplicationServer_OnEvent);
        static readonly Type Type = typeof(MyReplicationServer);
        static readonly Dictionary<uint, int> MethodIndices = new Dictionary<uint, int>();

        static readonly Type[] ParameterTypes =
        {
            typeof(MyPacketDataBitStreamBase),
            typeof(CallSite),
            typeof(object),
            typeof(IMyNetObject),
            typeof(Vector3D?),
            typeof(EndpointId),
        };

        static readonly MethodInfo Method = Type.GetMethod("OnEvent", BindingFlags, null, ParameterTypes, new ParameterModifier[0]);

        public static void Patch(PatchContext ctx)
        {
            try
            {
                var prefix = SelfType.GetStaticMethod(nameof(Prefix));
                var suffix = SelfType.GetStaticMethod(nameof(Suffix));

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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Prefix(object __instance, MyPacketDataBitStreamBase data, CallSite site, object obj, IMyNetObject sendAs, Vector3D? position, EndpointId source, ref ProfilerToken? __localProfilerHandle)
        {
            if (!MethodIndices.TryGetValue(site.Id, out var methodIndex))
            {
                var methodName = $"{Type.FullName}#OnEvent_{site.MethodInfo.Name}";
                methodIndex = StringIndexer.Instance.IndexOf(methodName);
                MethodIndices.Add(site.Id, methodIndex);
            }

            __localProfilerHandle = ProfilerPatch.StartToken(__instance, methodIndex, Category);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle);
        }
    }
}