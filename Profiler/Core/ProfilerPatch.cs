using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Havok;
using NLog;
using Profiler.Util;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using Torch.Utils.Reflected;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Entity.EntityComponents.Interfaces;
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

        [ReflectedMethodInfo(typeof(MyEntities), nameof(MyEntities.UpdateBeforeSimulation))]
        private static readonly MethodInfo _entitiesUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyEntities), nameof(MyEntities.UpdateAfterSimulation))]
        private static readonly MethodInfo _entitiesUpdateAfterSimulation;

        [ReflectedMethodInfo(typeof(MyEntities), nameof(MyEntities.UpdateOnceBeforeFrame))]
        private static readonly MethodInfo _entitiesUpdateOnceBeforeFrame;

        [ReflectedMethodInfo(typeof(Sandbox.Engine.Platform.Game), nameof(Sandbox.Engine.Platform.Game.RunSingleFrame))]
        private static readonly MethodInfo _gameRunSingleFrame;

        [ReflectedMethodInfo(typeof(MySession), nameof(MySession.UpdateComponents))]
        private static readonly MethodInfo _sessionUpdateComponents;

        [ReflectedFieldInfo(typeof(MyCubeGridSystems), "m_cubeGrid")]
        private static readonly FieldInfo _gridSystemsCubeGrid;

        [ReflectedMethodInfo(typeof(MyPhysics), "StepWorlds")]
        private static readonly MethodInfo _physicsStepWorlds;

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), "RunSandboxedProgramAction")]
        private static readonly MethodInfo _programmableRunSandboxed;
#pragma warning restore 649
        // ReSharper restore InconsistentNaming

        #endregion

        private static MethodInfo _distributedUpdaterIterate;

        public static void Patch(PatchContext ctx)
        {
            ReflectedManager.Process(typeof(ProfilerPatch));

            _distributedUpdaterIterate = typeof(MyDistributedUpdater<,>).GetMethod("Iterate");
            var duiP = _distributedUpdaterIterate?.GetParameters();
            if (_distributedUpdaterIterate == null || duiP == null || duiP.Length != 1 ||
                typeof(Action<>) != duiP[0].ParameterType.GetGenericTypeDefinition())
            {
                Log.Error(
                    $"Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Profiling will not function.  (Found {_distributedUpdaterIterate}");
                return;
            }

            PatchDistributedUpdate(ctx, _gameLogicUpdateBeforeSimulation);
            PatchDistributedUpdate(ctx, _gameLogicUpdateAfterSimulation);
            PatchDistributedUpdate(ctx, _entitiesUpdateBeforeSimulation);
            PatchDistributedUpdate(ctx, _entitiesUpdateAfterSimulation);

            {
                var patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MyGameLogicComponent));
                if (patcher == null)
                {
                    Log.Error($"Failed to make generic patching method for composite updates");
                }

                ctx.GetPattern(_gameLogicUpdateOnceBeforeFrame).PostTranspilers.Add(patcher);
                foreach (var type in new[] {"After", "Before"})
                foreach (var timing in new[] {1, 10, 100})
                {
                    var period = timing == 1 ? "" : timing.ToString();
                    var name = $"{typeof(IMyGameLogicComponent).FullName}.Update{type}Simulation{period}";
                    var method = typeof(MyCompositeGameLogicComponent).GetMethod(name,
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (method == null)
                    {
                        Log.Warn($"Failed to find {name} in CompositeGameLogicComponent.  Entity component profiling may not work.");
                        continue;
                    }

                    ctx.GetPattern(method).PostTranspilers.Add(patcher);
                }
            }

            {
                var patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MyEntity));
                if (patcher == null)
                {
                    Log.Error($"Failed to make generic patching method for entity update before frame");
                }

                ctx.GetPattern(_entitiesUpdateOnceBeforeFrame).PostTranspilers.Add(patcher);
            }

            {
                var patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MySessionComponentBase));
                if (patcher == null)
                    Log.Error($"Failed to make generic patching method for session components");

                ctx.GetPattern(_sessionUpdateComponents).PostTranspilers.Add(patcher);
            }

            ctx.GetPattern(_physicsStepWorlds).Prefixes.Add(ProfilerData.HandlePrefixPhysicsStepWorlds);

            ctx.GetPattern(_gameRunSingleFrame).Suffixes.Add(ProfilerData.DoTick);

            ctx.GetPattern(_programmableRunSandboxed).Prefixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(PrefixProfilePb)));
            ctx.GetPattern(_programmableRunSandboxed).Suffixes.Add(ReflectionUtils.StaticMethod(typeof(ProfilerPatch), nameof(SuffixProfilePb)));
        }

        // ReSharper disable InconsistentNaming
        // ReSharper disable once SuggestBaseTypeForParameter
        private static void PrefixProfilePb(MyProgrammableBlock __instance, ref MultiProfilerEntry __localProfilerHandle)
        {
            __localProfilerHandle = default(MultiProfilerEntry);
            ProfilerData.EntityEntry(__instance, ref __localProfilerHandle);
            __localProfilerHandle.Start();
        }

        private static void SuffixProfilePb(ref MultiProfilerEntry __localProfilerHandle)
        {
            __localProfilerHandle.Stop();
        }
        // ReSharper restore InconsistentNaming

        #region Generalized Update Transpiler

        private static bool ShouldProfileMethodCall<T>(MethodBase info)
        {
            if (info.IsStatic)
                return false;

            if (typeof(T) != typeof(MyCubeGridSystems) && !typeof(T).IsAssignableFrom(info.DeclaringType) &&
                (!typeof(MyGameLogicComponent).IsAssignableFrom(typeof(T)) || !typeof(IMyGameLogicComponent).IsAssignableFrom(info.DeclaringType)))
                return false;
            if (typeof(T) == typeof(MySessionComponentBase) && info.Name.Equals("Simulate", StringComparison.OrdinalIgnoreCase))
                return true;
            return info.Name.StartsWith("UpdateBeforeSimulation", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.StartsWith("UpdateAfterSimulation", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.StartsWith("UpdateOnceBeforeFrame", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<MsilInstruction> TranspilerForUpdate<T>(IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            MethodInfo profilerCall = null;
            if (typeof(IMyEntity).IsAssignableFrom(typeof(T)))
                profilerCall = ProfilerData.GetEntityProfiler;
            else if (typeof(MyEntityComponentBase).IsAssignableFrom(typeof(T)) ||
                     typeof(T) == typeof(IMyGameLogicComponent))
                profilerCall = ProfilerData.GetEntityComponentProfiler;
            else if (typeof(MyCubeGridSystems) == typeof(T))
                profilerCall = ProfilerData.GetGridSystemProfiler;
            else if (typeof(MySessionComponentBase) == typeof(T))
                profilerCall = ProfilerData.GetSessionComponentProfiler;
            else
                Log.Warn($"Trying to profile unknown target {typeof(T)}");

            var profilerEntry = profilerCall != null ? __localCreator(typeof(MultiProfilerEntry)) : null;

            var usedLocals = new List<MsilLocal>();
            var tmpArgument = new Dictionary<Type, Stack<MsilLocal>>();

            var foundAny = false;
            foreach (var i in instructions)
            {
                if (profilerCall != null && (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                    ShouldProfileMethodCall<T>((i.Operand as MsilOperandInline<MethodBase>)?.Value))
                {
                    var target = ((MsilOperandInline<MethodBase>) i.Operand).Value;
                    var parameters = target.GetParameters();
                    usedLocals.Clear();
                    foreach (var pam in parameters)
                    {
                        if (!tmpArgument.TryGetValue(pam.ParameterType, out var stack))
                            tmpArgument.Add(pam.ParameterType, stack = new Stack<MsilLocal>());
                        var local = stack.Count > 0 ? stack.Pop() : __localCreator(pam.ParameterType);
                        usedLocals.Add(local);
                        yield return local.AsValueStore();
                    }

                    Log.Debug(
                        $"Attaching profiling to {target?.DeclaringType?.FullName}#{target?.Name} in {__methodBase.DeclaringType?.FullName}#{__methodBase.Name} targeting {typeof(T)}");
                    yield return new MsilInstruction(OpCodes.Dup); // duplicate the object the update is called on
                    if (typeof(MyCubeGridSystems) == typeof(T) && __methodBase.DeclaringType == typeof(MyCubeGridSystems))
                    {
                        yield return new MsilInstruction(OpCodes.Ldarg_0);
                        yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_gridSystemsCubeGrid);
                    }

                    yield return profilerEntry.AsReferenceLoad();
                    yield return new MsilInstruction(OpCodes.Initobj).InlineValue(typeof(MultiProfilerEntry));
                    yield return profilerEntry.AsReferenceLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerCall);
                    yield return profilerEntry.AsReferenceLoad();
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(MultiProfilerEntry.ProfilerEntryStart);

                    for (var j = usedLocals.Count - 1; j >= 0; j--)
                    {
                        yield return usedLocals[j].AsValueLoad();
                        tmpArgument[usedLocals[j].Type].Push(usedLocals[j]);
                    }

                    yield return i;


                    yield return profilerEntry.AsReferenceLoad(); // stop the profiler
                    yield return new MsilInstruction(OpCodes.Call).InlineValue(MultiProfilerEntry.ProfilerEntryStop);
                    foundAny = true;
                    continue;
                }

                yield return i;
            }

            if (!foundAny)
                Log.Warn($"Didn't find any update profiling targets for {typeof(T)} in {__methodBase.DeclaringType?.FullName}#{__methodBase.Name}");
        }

        #endregion

        #region Distributed Update Targeting

        private static void PatchDistUpdateDel(PatchContext ctx, MethodBase method)
        {
            var pattern = ctx.GetPattern(method);
            var patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                .MakeGenericMethod(method.GetParameters()[0].ParameterType);
            if (patcher == null)
            {
                Log.Error($"Failed to make generic patching method for {method}");
            }

            pattern.PostTranspilers.Add(patcher);
        }

        private static bool IsDistributedIterate(MethodInfo info)
        {
            if (info == null)
                return false;
            if (!info.DeclaringType?.IsGenericType ?? true)
                return false;
            if (info.DeclaringType?.GetGenericTypeDefinition() != _distributedUpdaterIterate.DeclaringType)
                return false;
            var aps = _distributedUpdaterIterate.GetParameters();
            var ops = info.GetParameters();
            if (aps.Length != ops.Length)
                return false;
            for (var i = 0; i < aps.Length; i++)
                if (aps[i].ParameterType.GetGenericTypeDefinition() != ops[i].ParameterType.GetGenericTypeDefinition())
                    return false;
            return true;
        }

        private static void PatchDistributedUpdate(PatchContext ctx, MethodBase callerMethod)
        {
            var foundAnyIterate = false;
            var msil = PatchUtilities.ReadInstructions(callerMethod).ToList();
            for (var i = 0; i < msil.Count; i++)
            {
                var insn = msil[i];
                if ((insn.OpCode != OpCodes.Callvirt && insn.OpCode != OpCodes.Call) ||
                    !IsDistributedIterate((insn.Operand as MsilOperandInline<MethodBase>)?.Value as MethodInfo)) continue;

                foundAnyIterate = true;
                // Call to Iterate().  Backtrace up the instruction stack to find the statement creating the delegate.
                var foundNewDel = false;
                for (var j = i - 1; j >= 1; j--)
                {
                    var insn2 = msil[j];
                    if (insn2.OpCode != OpCodes.Newobj) continue;
                    var ctorType = (insn2.Operand as MsilOperandInline<MethodBase>)?.Value?.DeclaringType;
                    if (ctorType == null || !ctorType.IsGenericType || ctorType.GetGenericTypeDefinition() != typeof(Action<>)) continue;
                    foundNewDel = true;
                    // Find the instruction loading the function pointer this delegate is created with
                    var ldftn = msil[j - 1];
                    if (ldftn.OpCode != OpCodes.Ldftn ||
                        !(ldftn.Operand is MsilOperandInline<MethodBase> targetMethod))
                    {
                        Log.Error(
                            $"Unable to find ldftn instruction for call to Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                    }
                    else
                    {
                        Log.Debug(
                            $"Patching {targetMethod.Value.DeclaringType}#{targetMethod.Value} for {callerMethod.DeclaringType}#{callerMethod}");
                        PatchDistUpdateDel(ctx, targetMethod.Value);
                    }

                    break;
                }

                if (!foundNewDel)
                {
                    Log.Error(
                        $"Unable to find new Action() call for Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                }
            }

            if (!foundAnyIterate)
                Log.Error($"Unable to find any calls to {_distributedUpdaterIterate} in {callerMethod.DeclaringType}#{callerMethod}");
        }

        #endregion
    }
}