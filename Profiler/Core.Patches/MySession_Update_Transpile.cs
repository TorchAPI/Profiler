using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using ParallelTasks;
using Profiler.Util;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using TorchUtils;

namespace Profiler.Core.Patches
{
    public static class MySession_Update_Transpile
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(MySession_Update_Transpile);
        static readonly Type Type = typeof(MySession);
        static readonly MethodInfo Method = Type.InstanceMethod(nameof(MySession.Update));

        static readonly MethodInfo ParallelWaitTokenMethod = SelfType.StaticMethod(nameof(CreateTokenInParallelWait));
        static readonly MethodInfo ParallelRunTokenMethod = SelfType.StaticMethod(nameof(CreateTokenInParallelRun));

        static readonly (Type Type, string Method, MethodInfo TokenCreataor)[] TargetCalls =
        {
            (typeof(IWorkScheduler), nameof(IWorkScheduler.WaitForTasksToFinish), ParallelWaitTokenMethod),
            (typeof(Parallel), nameof(Parallel.RunCallbacks), ParallelRunTokenMethod),
        };

        static bool Matches(MethodBase method, Type type, string name)
        {
            return method.DeclaringType == type && method.Name == name;
        }

        static bool TryGetTokenCreatorMethod(MethodBase method, out MethodInfo tokenCreatorMethod)
        {
            if (TargetCalls.TryGetFirst(c => Matches(method, c.Type, c.Method), out var call))
            {
                tokenCreatorMethod = call.TokenCreataor;
                return true;
            }

            tokenCreatorMethod = default;
            return false;
        }

        public static void Patch(PatchContext ctx)
        {
            var transpiler = SelfType.StaticMethod(nameof(Transpiler));
            ctx.GetPattern(Method).PostTranspilers.Add(transpiler);
        }

        // ReSharper disable once InconsistentNaming
        static IEnumerable<MsilInstruction> Transpiler(IEnumerable<MsilInstruction> insns, Func<Type, MsilLocal> __localCreator)
        {
            var localTokenValue = __localCreator(typeof(ProfilerToken?));
            var oldInsns = insns.ToArray();
            var newInsns = insns.ToList();
            var insertedInsnCount = 0;

            for (var i = 0; i < oldInsns.Length; i++)
            {
                var insn = oldInsns[i];

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
                var mappingIndex = MethodIndexer.Instance.GetOrCreateIndexOf(method.DeclaringType, method.Name);

                // make a ProfilerToken instance
                var createTokenInsns = new List<MsilInstruction>
                {
                    new MsilInstruction(OpCodes.Ldc_I4).InlineValue(mappingIndex), // pass the method index to token
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
                    new MsilInstruction(OpCodes.Ldc_I4_1), // pass true (as in main thread)
                    new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopProfilerToken), // submit
                };

                newInsns.InsertRange(insertIndex, submitTokenInsns);
                insertedInsnCount += submitTokenInsns.Count;
            }

            return newInsns;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInParallelWait(int mappingIndex)
        {
            //Log.Trace($"session component: {obj?.GetType()}");
            return new ProfilerToken(null, mappingIndex, ProfilerCategory.UpdateParallelWait, DateTime.UtcNow);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? CreateTokenInParallelRun(int mappingIndex)
        {
            //Log.Trace($"replication layer: {obj?.GetType()}");
            return new ProfilerToken(null, mappingIndex, ProfilerCategory.UpdateParallelWait, DateTime.UtcNow);
        }
    }
}