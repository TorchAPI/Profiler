﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Util;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using Torch.Utils.Reflected;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;

namespace Profiler.Core
{
    [ReflectedLazy]
    internal static class ProfilerPatch
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        #region Patch Targets

        // ReSharper disable InconsistentNaming
#pragma warning disable 649
        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateBeforeSimulation))]
        private static readonly MethodInfo _gameLogicUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateAfterSimulation))]
        private static readonly MethodInfo _gameLogicUpdateAfterSimulation;

        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateOnceBeforeFrame))]
        private static readonly MethodInfo _gameLogicUpdateOnceBeforeFrame;

        [ReflectedMethodInfo(typeof(Sandbox.Engine.Platform.Game), nameof(Sandbox.Engine.Platform.Game.RunSingleFrame))]
        private static readonly MethodInfo _gameRunSingleFrame;

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), "RunSandboxedProgramAction")]
        private static readonly MethodInfo _programmableRunSandboxed;

        static readonly MethodInfo GetGenericProfilerToken = ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(Start));
        static readonly MethodInfo StopProfilerToken = ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(StopToken));
        static readonly MethodInfo DoTick = ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(Tick));

        private static readonly ListReader<string> ParallelEntityUpdateMethods = new List<string>
        {
            "UpdateBeforeSimulation",
            "UpdateBeforeSimulation10",
            "UpdateBeforeSimulation100",
            "ParallelUpdateHandlerAfterSimulation",
            "UpdateAfterSimulation",
            "UpdateAfterSimulation10",
            "UpdateAfterSimulation100",
            "DispatchOnceBeforeFrame",
            "DispatchSimulate",
        };

#pragma warning restore 649
        // ReSharper restore InconsistentNaming

        #endregion

        private static readonly MethodInfo _generalizedUpdateTranspiler = ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(TranspilerForUpdate));

        static readonly List<IProfiler> _observers = new List<IProfiler>();
        public static ulong CurrentTick { get; private set; }

        public const string GeneralEntrypoint = "General";
        public const string ScriptsEntrypoint = "Scripts";

        public static void Patch(PatchContext ctx)
        {
            Log.Trace("Profiler patch started");
            
            ReflectedManager.Process(typeof(ProfilerPatch));

            ctx.GetPattern(_gameRunSingleFrame).Suffixes.Add(DoTick);

            foreach (var parallelUpdateMethod in ParallelEntityUpdateMethods)
            {
                var method = typeof(MyParallelEntityUpdateOrchestrator).GetMethod(parallelUpdateMethod, ReflectionUtils.StaticFlags | ReflectionUtils.InstanceFlags);
                if (method != null)
                {
                    ctx.GetPattern(method).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }
                else
                {
                    Log.Error($"Unable to find {typeof(MyParallelEntityUpdateOrchestrator)}#{parallelUpdateMethod}.  Some profiling data will be missing");
                }
            }

            ctx.GetPattern(_gameLogicUpdateOnceBeforeFrame).PostTranspilers.Add(_generalizedUpdateTranspiler);

            if (MyDistributedUpdaterReflection.ApiExists())
            {
                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(_gameLogicUpdateBeforeSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }

                foreach (var updateMethod in MyDistributedUpdaterReflection.GetUpdateMethods(_gameLogicUpdateAfterSimulation))
                {
                    ctx.GetPattern(updateMethod).PostTranspilers.Add(_generalizedUpdateTranspiler);
                }
            }
            else
            {
                Log.Error("Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Some profiling data will be missing.");
            }

            ctx.GetPattern(_programmableRunSandboxed).Prefixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(PrefixProfilePb)));
            ctx.GetPattern(_programmableRunSandboxed).Suffixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(SuffixProfilePb)));
            
            Log.Trace("Profiler patch ended");
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable once SuggestBaseTypeForParameter
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void PrefixProfilePb(MyProgrammableBlock __instance, ref ProfilerToken? __localProfilerHandle)
        {
            __localProfilerHandle = StartProgrammableBlock(__instance);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SuffixProfilePb(ref ProfilerToken? __localProfilerHandle)
        {
            StopToken(__localProfilerHandle, true);
        }

        private static IEnumerable<MsilInstruction> TranspilerForUpdate(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = $"{__methodBase.DeclaringType?.FullName}#{__methodBase.Name}";
            Log.Trace($"Starting TranspilerForUpdate for method {methodBaseName}");
            
            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var il = instructions.ToList();

            var foundAny = false;
            for (var idx = 0; idx < il.Count; idx++)
            {
                var insn = il[idx];
                if ((insn.OpCode == OpCodes.Call || insn.OpCode == OpCodes.Callvirt) && insn.Operand is MsilOperandInline<MethodBase> methodOperand)
                {
                    var method = methodOperand.Value;
                    Log.Trace($"Found method {method.Name} (may not patch)");

                    if (method.Name.StartsWith("UpdateBeforeSimulation")
                        || method.Name.StartsWith("UpdateAfterSimulation")
                        || method.Name == "UpdateOnceBeforeFrame"
                        || method.Name == "Simulate")
                    {
                        var methodName = $"{method.DeclaringType?.FullName}#{method.Name}";
                        Log.Trace($"Matched method name {methodName} (may not patch)");

                        if (method.IsStatic)
                        {
                            Log.Error($"Failed attaching profiling to {methodName} in {methodBaseName}.  It's static");
                            continue;
                        }

                        // Valid to inject before this point
                        var methodCallPoint = idx;
                        var validInjectionPoint = methodCallPoint;
                        var additionalStackEntries = method.GetParameters().Length;
                        while (additionalStackEntries > 0)
                        {
                            additionalStackEntries -= il[--validInjectionPoint].StackChange();
                        }

                        if (additionalStackEntries < 0)
                        {
                            Log.Error(
                                $"Failed attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}."
                                + $"  Running back through the parameters left the stack in an invalid state.");
                            continue;
                        }

                        foundAny = true;
                        
                        Log.Trace($"Attaching profiling to {methodName} in {methodBaseName}#{__methodBase.Name}");
                        var startProfiler = new[]
                        {
                            new MsilInstruction(OpCodes.Dup), // duplicate the object the update is called on
                            // Grab a profiling token
                            new MsilInstruction(OpCodes.Call).InlineValue(GetGenericProfilerToken),
                            profilerEntry.AsValueStore(),
                        };

                        il.InsertRange(validInjectionPoint, startProfiler);
                        methodCallPoint += startProfiler.Length;

                        var stopProfiler = new[]
                        {
                            // Stop the profiler
                            profilerEntry.AsReferenceLoad(),
                            new MsilInstruction(method.Name.EndsWith("Parallel") ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1), // isMainThread
                            new MsilInstruction(OpCodes.Call).InlineValue(StopProfilerToken),
                        };

                        il.InsertRange(methodCallPoint + 1, stopProfiler);
                        idx = methodCallPoint + stopProfiler.Length - 1;
                    }
                }
            }

            if (!foundAny)
            {
                Log.Error($"Didn't find any update profiling targets for {methodBaseName}.  Some profiling data will be missing");
            }
            
            Log.Trace($"Finished TranspilerForUpdate for method {methodBaseName}");

            return il;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? Start(object obj)
        {
            switch (obj)
            {
                case MyEntityComponentBase componentBase:
                {
                    return new ProfilerToken(componentBase.Entity, GeneralEntrypoint, DateTime.UtcNow);
                }
                case IMyEntity entity:
                {
                    return new ProfilerToken(entity, GeneralEntrypoint, DateTime.UtcNow);
                }
                default:
                {
                    return null;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartProgrammableBlock(MyProgrammableBlock block)
        {
            return new ProfilerToken(block, ScriptsEntrypoint, DateTime.UtcNow);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void StopToken(in ProfilerToken? tokenOrNull, bool mainThreadUpdate)
        {
            if (!(tokenOrNull is ProfilerToken token)) return;

            var result = new ProfilerResult(
                token.GameEntity,
                token.Entrypoint,
                token.StartTimestamp,
                DateTime.UtcNow,
                mainThreadUpdate);

            lock (_observers)
            {
                foreach (var observer in _observers)
                {
                    observer.OnProfileComplete(result);
                }
            }
        }

        /// <summary>
        /// Add an observer to receive profiling data of every update method in the game world.
        /// </summary>
        /// <param name="observer">Observer object to receive profiling data until removed.</param>
        public static void AddProfiler(IProfiler observer)
        {
            lock (_observers)
            {
                if (_observers.Contains(observer))
                {
                    Log.Warn($"Observer already added: {observer}");
                    return;
                }

                _observers.Add(observer);
            }
        }

        /// <summary>
        /// Remove an observer to stop receiving profiling data.
        /// </summary>
        /// <param name="observer">Observer object to remove from the profiler.</param>
        public static void RemoveProfiler(IProfiler observer)
        {
            lock (_observers)
            {
                _observers.Remove(observer);
            }
        }

        /// <summary>
        /// Add an observer and, when the returned IDisposable object is disposed, remove the observer from the profiler.
        /// </summary>
        /// <param name="observer">Observer to add/remove.</param>
        /// <returns>IDisposable object that, when disposed, removes the observer from the profiler.</returns>
        public static IDisposable Profile(IProfiler observer)
        {
            AddProfiler(observer);
            return new ActionDisposable(() => RemoveProfiler(observer));
        }

        static void Tick()
        {
            CurrentTick++;
        }
    }
}
