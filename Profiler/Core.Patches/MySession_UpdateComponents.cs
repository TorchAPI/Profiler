﻿using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.TorchUtils;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;

namespace Profiler.Core.Patches
{
    public sealed class MySession_UpdateComponents
    {
        const ProfilerCategory Category = ProfilerCategory.UpdateSessionComponentsAll;
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MySession_UpdateComponents);
        static readonly Type Type = typeof(MySession);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MySession.UpdateComponents));
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Prefix(object __instance, ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = new ProfilerToken(null, MethodIndex, Category);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void Suffix(ref ProfilerToken? __localProfilerHandle)
        {
            ProfilerPatch.StopToken(in __localProfilerHandle, true);
        }
    }
}