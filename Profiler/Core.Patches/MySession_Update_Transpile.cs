﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
                var methodIndex = StringIndexer.Instance.IndexOf($"{method.DeclaringType}#{method.Name}");

                // make a ProfilerToken instance
                var createTokenInsns = new List<MsilInstruction>
                {
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