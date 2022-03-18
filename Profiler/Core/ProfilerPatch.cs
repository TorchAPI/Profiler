using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Torch.Utils.Reflected;

namespace Profiler.Core
{
    [ReflectedLazy]
    internal static class ProfilerPatch
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static readonly MethodInfo StopTokenFunc = typeof(ProfilerPatch).GetStaticMethod(nameof(StopToken));
        public static bool Enabled { get; set; } = true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ProfilerToken? StartToken(object caller, int methodIndex, ProfilerCategory category)
        {
            try
            {
                if (!Enabled) return null;
                var token = new ProfilerToken(caller, methodIndex, category);
                //Log.Trace($"start: {token}");
                return token;
            }
            catch (Exception e)
            {
                var method = StringIndexer.Instance.StringAt(methodIndex);
                Log.Error($"{e} caller: {caller}, method: {method}, category: {category}");
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StopToken(in ProfilerToken? tokenOrNull)
        {
            try
            {
                if (!Enabled) return;
                if (tokenOrNull is not { } token) return;

                var result = new ProfilerResult(token);
                ProfilerResultQueue.Enqueue(result);
                //Log.Trace($"end: {result}");
            }
            catch (Exception e)
            {
                Log.Error($"{e}; token: {tokenOrNull?.ToString() ?? "no token"}");
            }
        }
    }
}