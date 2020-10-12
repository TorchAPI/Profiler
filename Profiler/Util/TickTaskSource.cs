using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Profiler.Util
{
    public sealed class TickTaskSource
    {
        readonly List<TickTask> _incompleteTasks;

        public TickTaskSource()
        {
            _incompleteTasks = new List<TickTask>();
        }

        public TickTask GetTask()
        {
            var task = new TickTask();
            _incompleteTasks.Add(task);
            return task;
        }

        public void Tick(ulong tick)
        {
            foreach (var incompleteTask in _incompleteTasks)
            {
                incompleteTask.CompleteWithTick(tick);
            }

            _incompleteTasks.Clear();
        }

        public class TickTask : INotifyCompletion
        {
            ulong _result;
            Action _continuation;

            public TickTask GetAwaiter() => this;
            public bool IsCompleted { get; private set; }
            public ulong GetResult() => _result;

            public void OnCompleted(Action continuation)
            {
                if (_continuation != null)
                {
                    throw new Exception("Cannot await twice");
                }

                _continuation = continuation;
            }

            public void CompleteWithTick(ulong value)
            {
                if (IsCompleted) return;

                _result = value;
                IsCompleted = true;
                _continuation?.Invoke();
            }
        }
    }
}