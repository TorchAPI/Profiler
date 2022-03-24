using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Game.Components;
using VRage.Network;

namespace Profiler.Core.Patches
{
    public static class MySession_UpdateComponents_Transpile
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MySession_UpdateComponents_Transpile);
        static readonly Type Type = typeof(MySession);
        static readonly MethodInfo Method = Type.GetInstanceMethod(nameof(MySession.UpdateComponents));

        static readonly MethodInfo UpdateSessionComponentsCategoryTokenMethod = SelfType.GetStaticMethod(nameof(CreateTokenInUpdateSessionComponentsCategory));
        static readonly MethodInfo UpdateReplicationCategoryTokenMethod = SelfType.GetStaticMethod(nameof(CreateTokenInUpdateReplicationCategory));

        static readonly TranspileProfilePatcher TranspileProfilePatcher = new()
        {
            (typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdatedBeforeInit), UpdateSessionComponentsCategoryTokenMethod),
            (typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdateBeforeSimulation), UpdateSessionComponentsCategoryTokenMethod),
            (typeof(MyReplicationLayer), nameof(MyReplicationLayer.Simulate), UpdateReplicationCategoryTokenMethod),
            (typeof(MySessionComponentBase), nameof(MySessionComponentBase.Simulate), UpdateSessionComponentsCategoryTokenMethod),
            (typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdateAfterSimulation), UpdateSessionComponentsCategoryTokenMethod),
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
            Log.Trace("attaching profiler to MySessionComponentBase");
            return TranspileProfilePatcher.Patch(insns.ToArray(), __localCreator, __methodBase);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInUpdateSessionComponentsCategory(MySessionComponentBase obj, int methodIndex)
        {
            //Log.Trace($"session component: {obj?.GetType()}.{StringIndexer.Instance.StringAt(methodIndex)}");
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.UpdateSessionComponents);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInUpdateReplicationCategory(MySessionComponentBase obj, int methodIndex)
        {
            //Log.Trace($"replication layer: {obj?.GetType()}");
            return ProfilerPatch.StartToken(obj, methodIndex, ProfilerCategory.UpdateReplication);
        }
    }
}