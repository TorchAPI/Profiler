using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Utils;
using Sandbox.Engine.Platform;
using Torch.Managers.PatchManager;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core.Patches
{
    internal static class FixedLoop_Run
    {
        const ProfilerCategory Category = ProfilerCategory.Lock;
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly Type SelfType = typeof(FixedLoop_Run);
        static readonly Type Type = typeof(FixedLoop);
        static readonly MethodInfo Method = Type.GetInstanceMethod(nameof(FixedLoop.Run));
        static readonly int MethodIndex = StringIndexer.Instance.IndexOf($"{Type.FullName}#{Method.Name}");
        static readonly MethodInfo StartTokenFunc = SelfType.GetStaticMethod(nameof(StartToken));

        public static void Patch(PatchContext ctx)
        {
            var actionMethod = FindAction(Method);
            var transpiler = SelfType.GetStaticMethod(nameof(Transpile));
            ctx.GetPattern(actionMethod).PostTranspilers.Add(transpiler);
        }

        static MethodBase FindAction(MethodBase caller)
        {
            var msil = PatchUtilities.ReadInstructions(caller).ToList();
            for (var i = 0; i < msil.Count; i++)
            {
                Log.Trace($"insn: {i} {msil[i]}");

                var newobj = msil[i];
                if (newobj.OpCode != OpCodes.Newobj) continue;

                Log.Trace("newobj found");

                var newobjType = (newobj.Operand as MsilOperandInline<MethodBase>)?.Value?.DeclaringType;
                if (newobjType == null) continue;
                if (newobjType != typeof(GenericLoop.VoidAction)) continue;

                var ldftn = msil[i - 1];
                Log.Trace($"ldftn found: {ldftn}");

                if (ldftn.OpCode != OpCodes.Ldftn) continue;
                if (!(ldftn.Operand is MsilOperandInline<MethodBase> action)) continue;

                return action.Value;
            }

            throw new Exception("Failed to patch: action not found in FixedLoop.Run()");
        }

        static IEnumerable<MsilInstruction> Transpile(
            IEnumerable<MsilInstruction> instructions,
            // ReSharper disable once InconsistentNaming
            Func<Type, MsilLocal> __localCreator,
            // ReSharper disable once InconsistentNaming
            MethodBase __methodBase)
        {
            var methodBaseName = $"{__methodBase.DeclaringType?.FullName}#{__methodBase.Name}";
            Log.Trace($"Starting Transpile for method {methodBaseName}");

            var profilerEntry = __localCreator(typeof(ProfilerToken?));

            var msil = instructions.ToList();
            for (var i = 0; i < msil.Count; i++)
            {
                var insn = msil[i];
                if (insn.OpCode != OpCodes.Callvirt) continue;
                if (!(insn.Operand is MsilOperandInline<MethodBase> methodOperand)) continue;

                var method = methodOperand.Value;
                Log.Trace($"Found method {method.Name}");

                if (method.Name != "Wait") continue;

                var startProfiler = new[]
                {
                    new MsilInstruction(OpCodes.Call).InlineValue(StartTokenFunc), // Grab a profiling token
                    profilerEntry.AsValueStore(),
                };

                var stopProfiler = new[]
                {
                    // Stop the profiler
                    profilerEntry.AsReferenceLoad(),
                    new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc),
                };

                msil.InsertRange(i, startProfiler);
                msil.InsertRange(i + 1 + startProfiler.Length, stopProfiler);

                Log.Trace("result:");
                foreach (var m in msil)
                {
                    Log.Trace($"{m}");
                }

                return msil;
            }

            throw new Exception("Wait() not found");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ProfilerToken? StartToken()
        {
            return ProfilerPatch.StartToken(null, MethodIndex, Category);
        }
    }
}