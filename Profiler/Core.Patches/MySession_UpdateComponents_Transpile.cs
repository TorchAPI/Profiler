using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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

        static readonly ProfileBeginTokenTarget[] TargetCalls =
        {
            new ProfileBeginTokenTarget(typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdatedBeforeInit), UpdateSessionComponentsCategoryTokenMethod),
            new ProfileBeginTokenTarget(typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdateBeforeSimulation), UpdateSessionComponentsCategoryTokenMethod),
            new ProfileBeginTokenTarget(typeof(MyReplicationLayer), nameof(MyReplicationLayer.Simulate), UpdateReplicationCategoryTokenMethod),
            new ProfileBeginTokenTarget(typeof(MySessionComponentBase), nameof(MySessionComponentBase.Simulate), UpdateSessionComponentsCategoryTokenMethod),
            new ProfileBeginTokenTarget(typeof(MySessionComponentBase), nameof(MySessionComponentBase.UpdateAfterSimulation), UpdateSessionComponentsCategoryTokenMethod),
        };
        
        static bool TryGetTokenCreatorMethod(MethodBase method, out MethodInfo tokenCreatorMethod)
        {
            if (TargetCalls.TryGetFirst(c => c.Matches(method), out var call))
            {
                tokenCreatorMethod = call.TokenCreator;
                return true;
            }

            tokenCreatorMethod = default;
            return false;
        }

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

        // ReSharper disable once InconsistentNaming
        static IEnumerable<MsilInstruction> Transpile(IEnumerable<MsilInstruction> insns, Func<Type, MsilLocal> __localCreator)
        {
            var localTokenValue = __localCreator(typeof(ProfilerToken?));
            var oldInsns = insns.ToArray();
            var newInsns = new List<MsilInstruction>();
            var insertedInsnCount = 0;

            for (var i = 0; i < oldInsns.Length; i++)
            {
                var insn = oldInsns[i];
                newInsns.Add(insn);

                // skip any instructions other than method calls
                if (insn.OpCode != OpCodes.Call && insn.OpCode != OpCodes.Callvirt) continue;

                // shouldn't happen but anyway
                if (!(insn.Operand is MsilOperandInline<MethodBase> methodOperand)) continue;

                // skip any calls other than one of target calls
                var method = methodOperand.Value;

                Log.Trace($"method call: {method.DeclaringType?.FullName}{method.Name}");

                if (!TryGetTokenCreatorMethod(method, out var tokenCreatorMethod)) continue;

                Log.Trace("passed test");

                // get the index that the target stack begins
                var insertIndex = i + insertedInsnCount;

                Log.Trace($"index: {i}, insert index: {insertIndex}");

                // create a method index
                var methodIndex = StringIndexer.Instance.IndexOf($"{method.DeclaringType}#{method.Name}");

                // make a ProfilerToken instance
                var createTokenInsns = new List<MsilInstruction>
                {
                    new MsilInstruction(OpCodes.Dup), // copy & pass the caller object to token
                    new MsilInstruction(OpCodes.Ldc_I4).InlineValue(methodIndex), // pass the method index to token
                    new MsilInstruction(OpCodes.Call).InlineValue(tokenCreatorMethod), // create the token
                    localTokenValue.AsValueStore(), // store
                };

                // insert
                newInsns.InsertRange(insertIndex, createTokenInsns);
                insertedInsnCount += createTokenInsns.Count;

                // now is time to insert "submit token"
                insertIndex = i + insertedInsnCount + 1;

                // make a "submit token" call
                var submitTokenInsns = new List<MsilInstruction>
                {
                    localTokenValue.AsReferenceLoad(), // pass the token
                    new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc), // submit
                };

                newInsns.InsertRange(insertIndex, submitTokenInsns);
                insertedInsnCount += submitTokenInsns.Count;
            }

            return newInsns;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInUpdateSessionComponentsCategory(MySessionComponentBase obj, int methodIndex)
        {
            //Log.Trace($"session component: {obj?.GetType()}");
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