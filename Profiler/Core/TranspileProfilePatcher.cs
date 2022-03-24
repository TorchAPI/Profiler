using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using NLog;
using Profiler.Utils;
using Torch.Managers.PatchManager.MSIL;

namespace Profiler.Core
{
    public sealed class TranspileProfilePatcher : IEnumerable<(Type, string, MethodInfo)>
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly List<(Type, string, MethodInfo)> _candidates;

        public TranspileProfilePatcher()
        {
            _candidates = new List<(Type, string, MethodInfo)>();
        }

        public IEnumerator<(Type, string, MethodInfo)> GetEnumerator() => _candidates.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Add((Type DeclaryingTypeOrNull, string MethodNameRegex, MethodInfo StartToken) candidate)
        {
            _candidates.Add(candidate);
        }

        public IEnumerable<MsilInstruction> Patch(IReadOnlyList<MsilInstruction> insns, Func<Type, MsilLocal> localCreator, MethodBase methodBase)
        {
            var methodBaseName = NameMethod(methodBase);
            Log.Trace($"Transpile for method {methodBaseName}");

            Log.Trace("original:");

            foreach (var insn in insns)
            {
                Log.Trace(insn);
            }

            Log.Trace("original done");

            var stack = new Stack<MsilInstruction>();
            var foundAny = false;
            foreach (var insn in insns)
            {
                if (TryGetTokenCreator(insn, out var startToken))
                {
                    foundAny = true;
                    InsertProfiler(stack, insn, startToken, localCreator);
                }
                else
                {
                    stack.Push(insn);
                }
            }

            Log.Trace($"Transpile for method {methodBaseName} done");

            if (!foundAny)
            {
                Log.Error($"Didn't find any update profiling targets for {methodBaseName}.  Some profiling data will be missing");
                return insns;
            }

            Log.Trace("result:");

            var newInsns = stack.Reverse().ToArray();
            foreach (var insn in newInsns)
            {
                Log.Trace(insn);
            }

            Log.Trace("result done");

            return newInsns;
        }

        static void InsertProfiler(Stack<MsilInstruction> stack, MsilInstruction insn, MethodInfo startToken, Func<Type, MsilLocal> localCreator)
        {
            var method = ((MsilOperandInline<MethodBase>)insn.Operand).Value;
            var methodName = NameMethod(method);
            var methodIndex = StringIndexer.Instance.IndexOf(methodName);

            Log.Trace($"found inline method {methodName}");

            // push the caller
            var depth = method.GetParameters().Length + (method.IsStatic ? 0 : 1);
            if (startToken.GetParameters().Length == 1) // StartToken doesn't have the caller parameter
            {
                Log.Trace("inserted no caller");
            }
            else if (depth == 0) // static no-parameter method
            {
                if (startToken.GetParameters().Length > 1) // method doesn't have a caller but StartToken requires the caller
                {
                    stack.Push(new MsilInstruction(OpCodes.Ldnull));
                    Log.Trace("inserted null caller");
                }
                else
                {
                    Log.Trace("inserted no caller");
                }
            }
            else if (depth == 1) // duplicate the caller instruction as the 1st arg for StartToken
            {
                stack.Push(new MsilInstruction(OpCodes.Dup));
                Log.Trace($"inserted caller duplicate: {stack.Peek()}");
            }
            else // duplicate & rearrange
            {
                var argLocal = localCreator(typeof(object));
                var otherArgs = stack.Pop(depth - 1);
                stack.Push(new MsilInstruction(OpCodes.Dup));
                stack.Push(argLocal.AsValueStore());
                stack.PushAll(otherArgs);
                stack.Push(argLocal.AsValueLoad());
                Log.Trace($"inserted caller duplicate: {stack.Peek()}");
            }

            var profilerLocal = localCreator(typeof(ProfilerToken?));

            stack.PushAll(new[]
            {
                new MsilInstruction(OpCodes.Ldc_I4).InlineValue(methodIndex), // pass the method name as StartToken() 2nd arg
                new MsilInstruction(OpCodes.Call).InlineValue(startToken), // Grab a profiling token
                profilerLocal.AsValueStore(),
                insn,
                profilerLocal.AsReferenceLoad(),
                new MsilInstruction(OpCodes.Call).InlineValue(ProfilerPatch.StopTokenFunc),
            });
        }

        bool TryGetTokenCreator(MsilInstruction insn, out MethodInfo creator)
        {
            if (ReflectionUtils.TryGetInlineMethod(insn, out var targetMethod))
            {
                foreach (var (baseTypeOrNull, methodNameRegex, creatorCandidate) in _candidates)
                {
                    var testType = targetMethod.IsStatic || baseTypeOrNull == null || baseTypeOrNull == targetMethod.DeclaringType;
                    var testName = new Regex(methodNameRegex).IsMatch(targetMethod.Name);
                    if (testType && testName)
                    {
                        creator = creatorCandidate;
                        return true;
                    }
                }
            }

            creator = default;
            return false;
        }

        static string NameMethod(MethodBase method)
        {
            return $"{method.DeclaringType?.FullName}#{method.Name}";
        }
    }
}