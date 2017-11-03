using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NLog;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems;
using Sandbox.Game.GameSystems.Conveyors;
using Sandbox.Game.Weapons;
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
using VRage.Utils;

namespace Profiler.Impl
{
    [ReflectedLazy]
    internal static class ProfilerPatch
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        #region Patch Targets
#pragma warning disable 649
        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateBeforeSimulation))]
        private static readonly MethodInfo _gameLogicUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyGameLogic), nameof(MyGameLogic.UpdateAfterSimulation))]
        private static readonly MethodInfo _gameLogicUpdateAfterSimulation;

        [ReflectedMethodInfo(typeof(MyEntities), nameof(MyEntities.UpdateBeforeSimulation))]
        private static readonly MethodInfo _entitiesUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyEntities), nameof(MyEntities.UpdateAfterSimulation))]
        private static readonly MethodInfo _entitiesUpdateAfterSimulation;

        [ReflectedMethodInfo(typeof(Sandbox.Engine.Platform.Game), nameof(Sandbox.Engine.Platform.Game.RunSingleFrame))]
        private static readonly MethodInfo _gameRunSingleFrame;

        [ReflectedMethodInfo(typeof(MySession), nameof(MySession.UpdateComponents))]
        private static readonly MethodInfo _sessionUpdateComponents;


        #region CubeGridSystems
        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateBeforeSimulation))]
        private static readonly MethodInfo _cubeGridSystemsUpdateBeforeSimulation;

        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateBeforeSimulation10))]
        private static readonly MethodInfo _cubeGridSystemsUpdateBeforeSimulation10;

        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateBeforeSimulation100))]
        private static readonly MethodInfo _cubeGridSystemsUpdateBeforeSimulation100;

        //        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateAfterSimulation))]
        //        private static readonly MethodInfo _cubeGridSystemsUpdateAfterSimulation;
        //
        //        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateAfterSimulation10))]
        //        private static readonly MethodInfo _cubeGridSystemsUpdateAfterSimulation10;

        [ReflectedMethodInfo(typeof(MyCubeGridSystems), nameof(MyCubeGridSystems.UpdateAfterSimulation100))]
        private static readonly MethodInfo _cubeGridSystemsUpdateAfterSimulation100;
        #endregion

        [ReflectedFieldInfo(typeof(MyCubeGridSystems), "m_cubeGrid")]
        private static readonly FieldInfo _gridSystemsCubeGrid;

        #region Single Methods
        [ReflectedMethodInfo(typeof(MyCubeGrid), "UpdatePhysicsShape")]
        private static readonly MethodInfo _cubeGridUpdatePhysicsShape;

        [ReflectedMethodInfo(typeof(MyProgrammableBlock), nameof(MyProgrammableBlock.RunSandboxedProgramAction))]
        private static readonly MethodInfo _programmableBlockRunSandbox;

        [ReflectedMethodInfo(typeof(MyLargeTurretBase), "UpdateAiWeapon")]
        private static readonly MethodInfo _turretUpdateAiWeapon;

        [ReflectedMethodInfo(typeof(MySlimBlock), nameof(MySlimBlock.DoDamageInternal))]
        private static readonly MethodInfo _slimBlockDoDamageInternal;
        #endregion

#pragma warning restore 649
        #endregion

        private static MethodInfo _distributedUpdaterIterate;

        public static void Patch(PatchContext ctx)
        {
            ReflectedManager.Process(typeof(ProfilerPatch));

            _distributedUpdaterIterate = typeof(MyDistributedUpdater<,>).GetMethod("Iterate");
            ParameterInfo[] duiP = _distributedUpdaterIterate?.GetParameters();
            if (_distributedUpdaterIterate == null || duiP == null || duiP.Length != 1 || typeof(Action<>) != duiP[0].ParameterType.GetGenericTypeDefinition())
            {
                _log.Error(
                    $"Unable to find MyDistributedUpdater.Iterate(Delegate) method.  Profiling will not function.  (Found {_distributedUpdaterIterate}");
                return;
            }

            PatchDistributedUpdate(ctx, _gameLogicUpdateBeforeSimulation);
            PatchDistributedUpdate(ctx, _gameLogicUpdateAfterSimulation);
            PatchDistributedUpdate(ctx, _entitiesUpdateBeforeSimulation);
            PatchDistributedUpdate(ctx, _entitiesUpdateAfterSimulation);

            {
                MethodInfo patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MyCubeGridSystems));
                if (patcher == null)
                {
                    _log.Error($"Failed to make generic patching method for cube grid systems");
                }
                ctx.GetPattern(_cubeGridSystemsUpdateBeforeSimulation).Transpilers.Add(patcher);
                ctx.GetPattern(_cubeGridSystemsUpdateBeforeSimulation10).Transpilers.Add(patcher);
                ctx.GetPattern(_cubeGridSystemsUpdateBeforeSimulation100).Transpilers.Add(patcher);
                //                ctx.GetPattern(_cubeGridSystemsUpdateAfterSimulation).Transpilers.Add(patcher);
                //                ctx.GetPattern(_cubeGridSystemsUpdateAfterSimulation10).Transpilers.Add(patcher);
                ctx.GetPattern(_cubeGridSystemsUpdateAfterSimulation100).Transpilers.Add(patcher);
            }

            {
                MethodInfo patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MyGameLogicComponent));
                if (patcher == null)
                {
                    _log.Error($"Failed to make generic patching method for composite updates");
                }
                foreach (var type in new[] { "After", "Before" })
                    foreach (var timing in new[] { 1, 10, 100 })
                    {
                        var period = timing == 1 ? "" : timing.ToString();
                        var name = $"{typeof(IMyGameLogicComponent).FullName}.Update{type}Simulation{period}";
                        var method = typeof(MyCompositeGameLogicComponent).GetMethod(name,
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (method == null)
                        {
                            _log.Warn(
                                $"Failed to find {name} in CompositeGameLogicComponent.  Entity component profiling may not work.");
                            continue;
                        }
                        ctx.GetPattern(method).Transpilers.Add(patcher);
                    }
            }

            {
                MethodInfo patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                        BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                    .MakeGenericMethod(typeof(MySessionComponentBase));
                if (patcher == null)
                {
                    _log.Error($"Failed to make generic patching method for session components");
                }

                ctx.GetPattern(_sessionUpdateComponents).Transpilers.Add(patcher);
            }

            ctx.GetPattern(_gameRunSingleFrame).Suffixes.Add(ProfilerData.DoRotateEntries);


            var singleMethodProfiler = typeof(ProfilerPatch).GetMethod(nameof(TranspileSingleMethod),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            ctx.GetPattern(_cubeGridUpdatePhysicsShape).Transpilers.Add(singleMethodProfiler);
            ctx.GetPattern(_turretUpdateAiWeapon).Transpilers.Add(singleMethodProfiler);
            ctx.GetPattern(_programmableBlockRunSandbox).Transpilers.Add(singleMethodProfiler);
            ctx.GetPattern(_slimBlockDoDamageInternal).Transpilers.Add(singleMethodProfiler);
        }

        #region Single Method Transpiler Entry Providers
        // ReSharper disable UnusedMember.Local
        private static SlimProfilerEntry SingleMethodEntryProvider_Entity(IMyEntity __instance, string __key)
        {
            return ProfilerData.EntityEntry(__instance)?.GetSlim(__key);
        }

        private static SlimProfilerEntry SingleMethodEntryProvider_EntityComponent(MyEntityComponentBase __instance, string __key)
        {
            return ProfilerData.EntityComponentEntry(__instance)?.GetSlim(__key);
        }

        private static SlimProfilerEntry SingleMethodEntryProvider_SessionComponent(MySessionComponentBase __instance, string __key)
        {
            return ProfilerData.SessionComponentEntry(__instance)?.GetSlim(__key);
        }

        private static SlimProfilerEntry SingleMethodEntryProvider_GridConveyorRequest(IMyConveyorEndpointBlock __anyBlock, string __key)
        {
            if (__anyBlock is IMyEntity entity)
                return ProfilerData.EntityEntry(entity)?.GetSlim(__key);
            return null;
        }
        
        private static SlimProfilerEntry SingleMethodEntryProvider_SlimBlock_Damage(MySlimBlock __instance, MyStringHash damageType, string __key)
        {
            return ProfilerData.EntityEntry(__instance?.CubeGrid)?.GetFat("Damage")?.GetSlim(damageType.String);
        }
        // ReSharper restore UnusedMember.Local
        #endregion

        #region Single Method Transpiler
        private static readonly object _keyedStringPoolLock = new object();
        private static string[] _keyedStringPool = new string[64];
        private static int _usedStrings = 0;

        private static bool IsSingleMethodProfilerCall(MethodBase source, MethodInfo profilerCall)
        {
            if (!typeof(SlimProfilerEntry).IsAssignableFrom(profilerCall.ReturnType))
                return false;
            if (!profilerCall.IsStatic)
                return false;

            var pcarg = profilerCall.GetParameters();
            var sargs = source.GetParameters();
            foreach (var profilerArg in pcarg)
            {
                if (profilerArg.Name.Equals("__instance"))
                {
                    if (source.IsStatic || !profilerArg.ParameterType.IsAssignableFrom(source.DeclaringType))
                        return false;
                }
                else if (!profilerArg.Name.Equals("__key"))
                {
                    if (!sargs.Any(sourceArg =>
                            (sourceArg.Name.Equals(profilerArg.Name) || profilerArg.Name.StartsWith("__any")) &&
                                                profilerArg.ParameterType.IsAssignableFrom(sourceArg.ParameterType)))
                        return false;
                }
            }
            return true;
        }

        private static IEnumerable<MsilInstruction> EmitSingleMethodProfilerCall(MethodBase source,
            MethodInfo profilerCall, FieldInfo stringPool, int keyId)
        {
            var pcarg = profilerCall.GetParameters();
            var sargs = source.GetParameters();
            foreach (var profilerArg in pcarg)
            {
                if (profilerArg.Name.Equals("__instance"))
                {
                    yield return new MsilInstruction(OpCodes.Ldarg_0);
                }
                else if (profilerArg.Name.Equals("__key"))
                {
                    yield return new MsilInstruction(OpCodes.Ldsfld).InlineValue(stringPool);
                    yield return new MsilInstruction(OpCodes.Ldc_I4).InlineValue(keyId);
                    yield return new MsilInstruction(OpCodes.Ldelem_Ref);
                }
                else
                {
                    foreach (ParameterInfo sarg in sargs)
                        if (profilerArg.ParameterType.IsAssignableFrom(sarg.ParameterType) &&
                            (sarg.Name.Equals(profilerArg.Name) || profilerArg.Name.StartsWith("__any")))
                        {
                            yield return new MsilInstruction(OpCodes.Ldarg).InlineValue(new MsilArgument(sarg));
                            break;
                        }
                }
            }
            yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerCall);
        }

        private static IEnumerable<MsilInstruction> TranspileSingleMethod(IEnumerable<MsilInstruction> insn, Func<Type, MsilLocal> __localCreator, MethodBase __methodBase)
        {
            MethodInfo profilerCall = null;
            foreach (var method in typeof(ProfilerData).GetMethods(BindingFlags.Static | BindingFlags.NonPublic |
                                                                   BindingFlags.Public))
                if (IsSingleMethodProfilerCall(__methodBase, method))
                {
                    profilerCall = method;
                    break;
                }
            if (profilerCall == null)
                foreach (var method in typeof(ProfilerPatch).GetMethods(BindingFlags.Static | BindingFlags.NonPublic |
                                                                       BindingFlags.Public))
                    if (IsSingleMethodProfilerCall(__methodBase, method))
                    {
                        profilerCall = method;
                        break;
                    }
            if (profilerCall == null)
                _log.Warn($"Single method profiler for {__methodBase.DeclaringType?.FullName}#{__methodBase.Name} couldn't find a profiler call; will not operate");

            FieldInfo stringPool = typeof(ProfilerPatch).GetField(nameof(_keyedStringPool),
                BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (stringPool == null)
                _log.Warn($"Single method profiler for {__methodBase.DeclaringType?.FullName}#{__methodBase.Name} couldn't file string pool; will not operate");


            if (profilerCall == null || stringPool == null)
            {
                foreach (var i in insn)
                    yield return i;
                yield break;
            }

            // Reserve a keyed string.
            int stringKey = 0;
            lock (_keyedStringPoolLock)
            {
                while (_usedStrings >= _keyedStringPool.Length)
                    Array.Resize(ref _keyedStringPool, Math.Max(64, _keyedStringPool.Length * 2));
                stringKey = _usedStrings;
                _keyedStringPool[stringKey] = __methodBase.Name;
                _usedStrings++;
            }

            var profilerLocal = __localCreator(typeof(SlimProfilerEntry));

            _log.Debug($"Attaching profiling to {__methodBase?.DeclaringType?.FullName}#{__methodBase?.Name} with profiler call {profilerCall.DeclaringType?.FullName}#{profilerCall}");


            var labelNoProfiling = new MsilLabel();
            var labelStoreProfiling = new MsilLabel();
            yield return new MsilInstruction(OpCodes.Ldsfld).InlineValue(ProfilerData.FieldProfileSingleMethods);
            yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(labelNoProfiling);
            { // if (ProfilerData.FieldProfileSingleMethods)
                foreach (var stub in EmitSingleMethodProfilerCall(__methodBase, profilerCall, stringPool, stringKey))
                    yield return stub;
                yield return new MsilInstruction(OpCodes.Br).InlineTarget(labelStoreProfiling);
            }
            { // else
                yield return new MsilInstruction(OpCodes.Ldnull).LabelWith(labelNoProfiling);
            }
            yield return new MsilInstruction(OpCodes.Dup).LabelWith(labelStoreProfiling); // Duplicate profiler entry for brnull
            yield return profilerLocal.AsValueStore(); // store the profiler entry for later

            var skipProfilerOne = new MsilLabel();
            yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(skipProfilerOne);
            { // if (profiler != null)
                yield return profilerLocal.AsValueLoad();
                yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.ProfilerEntryStart);
            }

            // consumes from the first Dup
            yield return new MsilInstruction(OpCodes.Nop).LabelWith(skipProfilerOne);

            var skipMainMethod = new MsilLabel();
            foreach (var i in insn)
            {
                if (i.OpCode == OpCodes.Ret)
                {
                    MsilInstruction j = new MsilInstruction(OpCodes.Br).InlineTarget(skipMainMethod);
                    foreach (MsilLabel l in i.Labels)
                        j.Labels.Add(l);
                    yield return j;
                }
                else
                {
                    yield return i;
                }
            }

            var skipProfilerTwo = new MsilLabel();
            yield return profilerLocal.AsValueLoad().LabelWith(skipMainMethod);
            yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(skipProfilerTwo); // Brfalse == Brnull
            {
                yield return profilerLocal.AsValueLoad(); // stop the profiler
                yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.ProfilerEntryStop);
            }
            yield return new MsilInstruction(OpCodes.Nop).LabelWith(skipProfilerTwo);
        }
        #endregion

        #region Generalized Update Transpiler
        private static bool ShouldProfileMethodCall<T>(MethodBase info)
        {
            if (info.IsStatic)
                return false;

            if (typeof(T) != typeof(MyCubeGridSystems) &&
                !typeof(T).IsAssignableFrom(info.DeclaringType) &&
                (!typeof(MyGameLogicComponent).IsAssignableFrom(typeof(T)) || typeof(IMyGameLogicComponent) != info.DeclaringType))
                return false;
            if (typeof(T) == typeof(MySessionComponentBase) && info.Name.Equals("Simulate", StringComparison.OrdinalIgnoreCase))
                return true;
            return info.Name.StartsWith("UpdateBeforeSimulation", StringComparison.OrdinalIgnoreCase) ||
                   info.Name.StartsWith("UpdateAfterSimulation", StringComparison.OrdinalIgnoreCase);
        }

        private static IEnumerable<MsilInstruction> TranspilerForUpdate<T>(IEnumerable<MsilInstruction> insn, Func<Type, MsilLocal> __localCreator, MethodBase __methodBase)
        {
            MethodInfo profilerCall = null;
            if (typeof(IMyEntity).IsAssignableFrom(typeof(T)))
                profilerCall = ProfilerData.GetEntityProfiler;
            else if (typeof(MyEntityComponentBase).IsAssignableFrom(typeof(T)) || typeof(T) == typeof(IMyGameLogicComponent))
                profilerCall = ProfilerData.GetEntityComponentProfiler;
            else if (typeof(MyCubeGridSystems) == typeof(T))
                profilerCall = ProfilerData.GetGridSystemProfiler;
            else if (typeof(MySessionComponentBase) == typeof(T))
                profilerCall = ProfilerData.GetSessionComponentProfiler;
            else
                _log.Warn($"Trying to profile unknown target {typeof(T)}");

            MsilLocal profilerEntry = profilerCall != null
                ? __localCreator(typeof(SlimProfilerEntry))
                : null;

            var usedLocals = new List<MsilLocal>();
            var tmpArgument = new Dictionary<Type, Stack<MsilLocal>>();

            var foundAny = false;
            foreach (MsilInstruction i in insn)
            {
                if (profilerCall != null && (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt) &&
                    ShouldProfileMethodCall<T>((i.Operand as MsilOperandInline<MethodBase>)?.Value))
                {
                    MethodBase target = ((MsilOperandInline<MethodBase>)i.Operand).Value;
                    ParameterInfo[] pams = target.GetParameters();
                    usedLocals.Clear();
                    foreach (ParameterInfo pam in pams)
                    {
                        if (!tmpArgument.TryGetValue(pam.ParameterType, out var stack))
                            tmpArgument.Add(pam.ParameterType, stack = new Stack<MsilLocal>());
                        MsilLocal local = stack.Count > 0 ? stack.Pop() : __localCreator(pam.ParameterType);
                        usedLocals.Add(local);
                        yield return local.AsValueStore();
                    }

                    _log.Debug($"Attaching profiling to {target?.DeclaringType?.FullName}#{target?.Name} in {__methodBase.DeclaringType?.FullName}#{__methodBase.Name} targeting {typeof(T)}");
                    yield return new MsilInstruction(OpCodes.Dup); // duplicate the object the update is called on
                    if (typeof(MyCubeGridSystems) == typeof(T) && __methodBase.DeclaringType == typeof(MyCubeGridSystems))
                    {
                        yield return new MsilInstruction(OpCodes.Ldarg_0);
                        yield return new MsilInstruction(OpCodes.Ldfld).InlineValue(_gridSystemsCubeGrid);
                    }

                    yield return new MsilInstruction(OpCodes.Call).InlineValue(profilerCall); // consume object the update is called on
                    yield return new MsilInstruction(OpCodes.Dup); // Duplicate profiler entry for brnull
                    yield return profilerEntry.AsValueStore(); // store the profiler entry for later

                    var skipProfilerOne = new MsilLabel();
                    yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(skipProfilerOne); // Brfalse == Brnull
                    {
                        yield return profilerEntry.AsValueLoad(); // start the profiler
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.ProfilerEntryStart);
                    }

                    // consumes from the first Dup
                    yield return new MsilInstruction(OpCodes.Nop).LabelWith(skipProfilerOne);
                    for (int j = usedLocals.Count - 1; j >= 0; j--)
                    {
                        yield return usedLocals[j].AsValueLoad();
                        tmpArgument[usedLocals[j].Type].Push(usedLocals[j]);
                    }
                    yield return i;

                    var skipProfilerTwo = new MsilLabel();
                    yield return profilerEntry.AsValueLoad();
                    yield return new MsilInstruction(OpCodes.Brfalse).InlineTarget(skipProfilerTwo); // Brfalse == Brnull
                    {
                        yield return profilerEntry.AsValueLoad(); // stop the profiler
                        yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerData.ProfilerEntryStop);
                    }
                    yield return new MsilInstruction(OpCodes.Nop).LabelWith(skipProfilerTwo);
                    foundAny = true;
                    continue;
                }
                yield return i;
            }
            if (!foundAny)
                _log.Warn($"Didn't find any update profiling targets for target {typeof(T)} in {__methodBase.DeclaringType?.FullName}#{__methodBase.Name}");
        }
        #endregion

        #region Distributed Update Targeting
        private static void PatchDistUpdateDel(PatchContext ctx, MethodBase method)
        {
            MethodRewritePattern pattern = ctx.GetPattern(method);
            MethodInfo patcher = typeof(ProfilerPatch).GetMethod(nameof(TranspilerForUpdate),
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?
                .MakeGenericMethod(method.GetParameters()[0].ParameterType);
            if (patcher == null)
            {
                _log.Error($"Failed to make generic patching method for {method}");
            }
            pattern.Transpilers.Add(patcher);
        }

        private static bool IsDistributedIterate(MethodInfo info)
        {
            if (info == null)
                return false;
            if (!info.DeclaringType?.IsGenericType ?? true)
                return false;
            if (info.DeclaringType?.GetGenericTypeDefinition() != _distributedUpdaterIterate.DeclaringType)
                return false;
            ParameterInfo[] aps = _distributedUpdaterIterate.GetParameters();
            ParameterInfo[] ops = info.GetParameters();
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
            List<MsilInstruction> msil = PatchUtilities.ReadInstructions(callerMethod).ToList();
            for (var i = 0; i < msil.Count; i++)
            {
                MsilInstruction insn = msil[i];
                if ((insn.OpCode == OpCodes.Callvirt || insn.OpCode == OpCodes.Call)
                    && IsDistributedIterate((insn.Operand as MsilOperandInline<MethodBase>)?.Value as MethodInfo))
                {
                    foundAnyIterate = true;
                    // Call to Iterate().  Backtrace up the instruction stack to find the statement creating the delegate.
                    var foundNewDel = false;
                    for (int j = i - 1; j >= 1; j--)
                    {
                        MsilInstruction insn2 = msil[j];
                        if (insn2.OpCode == OpCodes.Newobj)
                        {
                            Type ctorType = (insn2.Operand as MsilOperandInline<MethodBase>)?.Value?.DeclaringType;
                            if (ctorType != null && ctorType.IsGenericType &&
                                ctorType.GetGenericTypeDefinition() == typeof(Action<>))
                            {
                                foundNewDel = true;
                                // Find the instruction loading the function pointer this delegate is created with
                                MsilInstruction ldftn = msil[j - 1];
                                if (ldftn.OpCode != OpCodes.Ldftn ||
                                    !(ldftn.Operand is MsilOperandInline<MethodBase> targetMethod))
                                {
                                    _log.Error(
                                        $"Unable to find ldftn instruction for call to Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                                }
                                else
                                {
                                    _log.Debug($"Patching {targetMethod.Value.DeclaringType}#{targetMethod.Value} for {callerMethod.DeclaringType}#{callerMethod}");
                                    PatchDistUpdateDel(ctx, targetMethod.Value);
                                }
                                break;
                            }
                        }
                    }
                    if (!foundNewDel)
                    {
                        _log.Error($"Unable to find new Action() call for Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                    }
                }
            }
            if (!foundAnyIterate)
                _log.Error($"Unable to find any calls to {_distributedUpdaterIterate} in {callerMethod.DeclaringType}#{callerMethod}");
        }
        #endregion
    }
}
