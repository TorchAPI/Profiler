using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Havok;
using Profiler.Basics;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Utils;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using Torch.Utils;
using VRage;
using VRage.Library.Utils;
using VRageMath.Spatial;

namespace Profiler.Core.Patches
{
    public static class MyPhysics_StepWorlds
    {
        [ReflectedMethodInfo(typeof(MyPhysics_StepWorlds), nameof(StartToken))]
#pragma warning disable 649
        private static readonly MethodInfo _startTokenMethod;

        [ReflectedMethodInfo(typeof(MyPhysics), "StepWorldsParallel")]
        private static readonly MethodInfo _stepWorldsParallelMethod;

        [ReflectedMethodInfo(typeof(MyPhysics), "StepWorldsInternal")]
        private static readonly MethodInfo _stepWorldsMethod;

        [ReflectedMethodInfo(typeof(MyPhysics_StepWorlds), nameof(StepWorldsPrefix))]
        private static readonly MethodInfo _stepWorldsPrefixMethod;

        [ReflectedMethodInfo(typeof(MyPhysics_StepWorlds), nameof(StepWorldsParallelTranspiler))]
        private static readonly MethodInfo _stepWorldsParallelTranspilerMethod;
#pragma warning restore 649

        private static Action _stepWorldsParallel;
        
        private static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{typeof(MyPhysics).FullName}#StepWorlds");
        
        public static void Patch(PatchContext ctx)
        {
            ctx.GetPattern(_stepWorldsMethod).Prefixes.Add(_stepWorldsPrefixMethod);
            ctx.GetPattern(_stepWorldsParallelMethod).Transpilers.Add(_stepWorldsParallelTranspilerMethod);
        }

        private static bool StepWorldsPrefix(MyPhysics __instance)
        {
            _stepWorldsParallel ??= _stepWorldsParallelMethod.CreateDelegate<Action>(__instance);
            
            if (ClusterTreeProfiler.Active)
            {
                MyPhysics.ProfileHkCall(_stepWorldsParallel);
            }
            else
            {
                _stepWorldsParallel();
            }
            
            if (HkBaseSystem.IsOutOfMemory)
            {
                throw new OutOfMemoryException("Havok run out of memory");
            }
            return false;
        }
        
        private static IEnumerable<MsilInstruction> StepWorldsParallelTranspiler(IEnumerable<MsilInstruction> ins, Func<Type, MsilLocal> __localCreator)
        {
            var tokenStore = __localCreator(typeof(ProfilerToken?));
            var initFound = false;
            var finishFound = false;
            foreach (var instruction in ins)
            {
                if (instruction.OpCode == OpCodes.Pop) continue;
                if (instruction.OpCode == OpCodes.Callvirt &&
                    instruction.Operand is MsilOperandInline.MsilOperandReflected<MethodBase> operand)
                {
                    switch (operand.Value.Name)
                    {
                        case "InitMtStep":
                            // call virt
                            yield return instruction;
                            // pop
                            yield return new MsilInstruction(OpCodes.Pop);
                            // load cluster
                            yield return new MsilInstruction(OpCodes.Ldloc_S).InlineValue(new MsilLocal(4));
                            // create token
                            yield return new MsilInstruction(OpCodes.Call).InlineValue(_startTokenMethod);
                            // save token to local ver
                            yield return tokenStore.AsValueStore();
                        
                            initFound = true;
                            continue;
                        case "FinishMtStep":
                            // call virt
                            yield return instruction;
                            // pop
                            yield return new MsilInstruction(OpCodes.Pop);
                            // load saved token
                            yield return tokenStore.AsReferenceLoad();
                            // finish token
                            yield return new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc);
                        
                            finishFound = true;
                            continue;
                    }
                }
                yield return instruction;
            }

            if (!initFound || !finishFound)
                throw new MissingMemberException();
        }

        private static ProfilerToken? StartToken(MyClusterTree.MyCluster cluster)
        {
            return ProfilerPatch.StartToken(cluster, MethodIndex, ProfilerCategory.General);
        }
    }
}