using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using NLog;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;
using VRage.Collections;

namespace Profiler.Core.Patches
{
    public static class MyDistributedUpdater_Iterate
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();
        static readonly MethodInfo Method = typeof(MyDistributedUpdater<,>).GetMethod("Iterate");

        public static bool ApiExists()
        {
            var duiP = Method?.GetParameters();

            return Method != null &&
                   duiP != null &&
                   duiP.Length == 1 &&
                   typeof(Action<>) == duiP[0].ParameterType.GetGenericTypeDefinition();
        }

        static bool IsDistributedIterate(MethodInfo info)
        {
            if (info == null) return false;
            if (!info.DeclaringType?.IsGenericType ?? true) return false;
            if (info.DeclaringType?.GetGenericTypeDefinition() != Method.DeclaringType) return false;

            var aps = Method.GetParameters();
            var ops = info.GetParameters();
            if (aps.Length != ops.Length) return false;

            for (var i = 0; i < aps.Length; i++)
            {
                if (aps[i].ParameterType.GetGenericTypeDefinition() != ops[i].ParameterType.GetGenericTypeDefinition())
                {
                    return false;
                }
            }

            return true;
        }

        public static IEnumerable<MethodBase> FindUpdateMethods(MethodBase callerMethod)
        {
            var updateMethods = new List<MethodBase>();

            var foundAnyIterate = false;
            var msil = PatchUtilities.ReadInstructions(callerMethod).ToList();
            for (var i = 0; i < msil.Count; i++)
            {
                var insn = msil[i];
                if (insn.OpCode != OpCodes.Callvirt && insn.OpCode != OpCodes.Call) continue;
                if (!IsDistributedIterate((insn.Operand as MsilOperandInline<MethodBase>)?.Value as MethodInfo)) continue;

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
                    if (ldftn.OpCode != OpCodes.Ldftn || !(ldftn.Operand is MsilOperandInline<MethodBase> targetMethod))
                    {
                        Log.Error($"Unable to find ldftn instruction for call to Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                    }
                    else
                    {
                        Log.Debug($"Patching {targetMethod.Value.DeclaringType}#{targetMethod.Value} for {callerMethod.DeclaringType}#{callerMethod}");
                        updateMethods.Add(targetMethod.Value);
                    }

                    break;
                }

                if (!foundNewDel)
                {
                    Log.Error($"Unable to find new Action() call for Iterate in {callerMethod.DeclaringType}#{callerMethod}");
                }
            }

            if (!foundAnyIterate)
            {
                Log.Error($"Unable to find any calls to {Method} in {callerMethod.DeclaringType}#{callerMethod}");
            }

            return updateMethods;
        }
    }
}