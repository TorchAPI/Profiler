﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Havok;
using NLog;
using Profiler.Utils;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Utils;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Collections;

namespace Profiler.Core.Patches
{
    public static class MyPhysics_StepWorlds
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{typeof(MyPhysics).FullName}#StepSingleWorld");
        static readonly MyConcurrentHashSet<object> _flags = new();

        static bool _simulatesParallel;

        public static void FlagContinuous(object locker)
        {
            _flags.Add(locker);
            _simulatesParallel = _flags.Count == 0;
        }

        public static void UnflagContinuous(object locker)
        {
            _flags.Remove(locker);
            _simulatesParallel = _flags.Count == 0;
        }

        public static void Patch(PatchContext ctx)
        {
            var stepWorldsInternalPatchee = typeof(MyPhysics).GetInstanceMethod("StepWorldsInternal");
            var stepWorldsInternalPatcher = typeof(MyPhysics_StepWorlds).GetStaticMethod(nameof(StepWorldsInternalTranspiler));
            ctx.GetPattern(stepWorldsInternalPatchee).Transpilers.Add(stepWorldsInternalPatcher);

            var stepSingleWorldPatchee = typeof(MyPhysics).GetInstanceMethod("StepSingleWorld");
            var stepSingleWorldPrefix = typeof(MyPhysics_StepWorlds).GetStaticMethod(nameof(StepSingleWorldPrefix));
            var stepSingleWorldSuffix = typeof(MyPhysics_StepWorlds).GetStaticMethod(nameof(StepSingleWorldSuffix));
            ctx.GetPattern(stepSingleWorldPatchee).Prefixes.Add(stepSingleWorldPrefix);
            ctx.GetPattern(stepSingleWorldPatchee).Suffixes.Add(stepSingleWorldSuffix);
        }

        static IEnumerable<MsilInstruction> StepWorldsInternalTranspiler(IEnumerable<MsilInstruction> insns)
        {
            var foundField = false;

            foreach (var insn in insns)
            {
                if (insn.OpCode == OpCodes.Ldsfld &&
                    insn.Operand is MsilOperandInline<FieldInfo> field &&
                    field.Value.Name == nameof(MyFakes.ENABLE_HAVOK_PARALLEL_SCHEDULING))
                {
                    var newField = typeof(MyPhysics_StepWorlds).GetField(nameof(_simulatesParallel), BindingFlags.Static | BindingFlags.NonPublic);
                    var newInsn = insn.CopyWith(OpCodes.Ldsfld).InlineValue(newField);
                    yield return newInsn;

                    foundField = true;
                }
                else
                {
                    yield return insn;
                }
            }

            if (!foundField)
            {
                throw new InvalidOperationException("Multithreading field not found");
            }
        }

        // ReSharper disable once RedundantAssignment
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void StepSingleWorldPrefix(ref HkWorld world, ref ProfilerToken? __localProfilerHandle)
        {
            if (_simulatesParallel) return;
            __localProfilerHandle = ProfilerPatch.StartToken(world, MethodIndex, ProfilerCategory.Physics);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void StepSingleWorldSuffix(ref ProfilerToken? __localProfilerHandle)
        {
            if (_simulatesParallel) return;
            ProfilerPatch.StopToken(in __localProfilerHandle);
        }
    }
}