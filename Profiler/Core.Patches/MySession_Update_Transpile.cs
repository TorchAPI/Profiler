using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using ParallelTasks;
using Profiler.Utils;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core.Patches
{
    public static class MySession_Update_Transpile
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MySession_Update_Transpile);
        static readonly Type Type = typeof(MySession);
        static readonly MethodInfo Method = Type.GetInstanceMethod(nameof(MySession.Update));

        static readonly MethodInfo ParallelWaitTokenMethod = SelfType.GetStaticMethod(nameof(CreateTokenInParallelWait));
        static readonly MethodInfo ParallelRunTokenMethod = SelfType.GetStaticMethod(nameof(CreateTokenInParallelRun));

        static readonly TranspileProfilePatcher TranspileProfilePatcher = new()
        {
            (typeof(IWorkScheduler), nameof(IWorkScheduler.WaitForTasksToFinish), ParallelWaitTokenMethod),
            (typeof(Parallel), nameof(Parallel.RunCallbacks), ParallelRunTokenMethod),
        };

        public static void Patch(PatchContext ctx)
        {
            try
            {
                var transpiler = SelfType.GetStaticMethod(nameof(Transpile));
                ctx.GetPattern(Method).PostTranspilers.Add(transpiler);
            }
            catch (Exception e)
            {
                Log.Error($"Failed to patch: {e.Message}");
            }
        }

        static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> insns, Func<Type, MsilLocal> __localCreator, MethodBase __methodBase)
        {
            return TranspileProfilePatcher.Patch(insns.ToArray(), __localCreator, __methodBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInParallelWait(int methodIndex)
        {
            //Log.Trace($"session component: {obj?.GetType()}");
            return ProfilerPatch.StartToken(null, methodIndex, ProfilerCategory.UpdateParallelWait);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInParallelRun(int methodIndex)
        {
            //Log.Trace($"replication layer: {obj?.GetType()}");
            return ProfilerPatch.StartToken(null, methodIndex, ProfilerCategory.UpdateParallelRun);
        }
    }
}