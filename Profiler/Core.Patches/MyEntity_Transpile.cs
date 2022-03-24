using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Profiler.Utils;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core.Patches
{
    public static class MyEntity_Transpile
    {
        static readonly Type SelfType = typeof(MyEntity_Transpile);
        static readonly MethodInfo StartTokenFunc = SelfType.GetStaticMethod(nameof(StartToken));
        static readonly MethodInfo Transpiler = SelfType.GetStaticMethod(nameof(Transpile));

        static readonly TranspileProfilePatcher TranspileProfilePatcher = new()
        {
            (null, "^UpdateBeforeSimulation.*$", StartTokenFunc),
            (null, "^UpdateAfterSimulation.*$", StartTokenFunc),
            (null, "^UpdateOnceBeforeFrame$", StartTokenFunc),
            (null, "^Simulate$", StartTokenFunc),
        };

        // profile all Update methods found inside given method
        public static void Patch(PatchContext ctx, MethodBase method)
        {
            ctx.GetPattern(method).PostTranspilers.Add(Transpiler);
        }

        static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> insns, Func<Type, MsilLocal> __localCreator, MethodBase __methodBase)
        {
            return TranspileProfilePatcher.Patch(insns.ToArray(), __localCreator, __methodBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartToken(object obj, int methodIndex)
        {
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.General);
        }
    }
}