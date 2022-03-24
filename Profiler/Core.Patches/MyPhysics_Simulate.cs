using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Havok;
using NLog;
using Profiler.Utils;
using Sandbox.Engine.Physics;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core.Patches
{
    public static class MyPhysics_Simulate
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly MethodInfo StartTokenFunc = typeof(MyPhysics_Simulate).GetStaticMethod(nameof(StartToken));
        static readonly MethodInfo StartMtTokenFunc = typeof(MyPhysics_Simulate).GetStaticMethod(nameof(StartMtToken));

        static readonly TranspileProfilePatcher TranspileProfilePatcher = new()
        {
            (null, "^ExecuteJobQueue$", StartTokenFunc),
            (null, "^IsClusterActive$", StartTokenFunc),
            (null, "^ProcessAllJobs$", StartTokenFunc),
            (null, "^WaitForCompletion$", StartTokenFunc),
            (null, "^FinishMtStep$", StartMtTokenFunc),
        };

        public static void Patch(PatchContext ctx)
        {
            var patchee = typeof(MyPhysics).GetInstanceMethod("StepWorldsParallel");
            var patcher = typeof(MyPhysics_Simulate).GetStaticMethod(nameof(Transpile));
            ctx.GetPattern(patchee).PostTranspilers.Add(patcher);
        }

        static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> insns, Func<Type, MsilLocal> __localCreator, MethodBase __methodBase)
        {
            return TranspileProfilePatcher.Patch(insns.ToArray(), __localCreator, __methodBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartToken(object obj, int methodIndex)
        {
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.PhysicsSimulate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartMtToken(HkWorld world, int methodIndex)
        {
            return ProfilerPatch.StartToken(world, methodIndex, ProfilerCategory.PhysicsSimulate);
        }
    }
}