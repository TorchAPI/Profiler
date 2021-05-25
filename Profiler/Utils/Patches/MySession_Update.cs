using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using Torch.Utils;

namespace Profiler.Utils.Patches
{
    [PatchShim]
    public static class MySession_Update
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

#pragma warning disable 649
        [ReflectedMethodInfo(typeof(MySession), nameof(MySession.Update))]
        static readonly MethodInfo _MySessionUpdate;

        [ReflectedMethodInfo(typeof(MySession_Update), nameof(MySessionUpdatePatch))]
        static readonly MethodInfo _MySessionUpdatePatch;
#pragma warning restore 649

        static readonly ConcurrentQueue<Action> _actionQueue;
        static readonly List<Action> _actionQueueCopy;

        static MySession_Update()
        {
            _actionQueue = new ConcurrentQueue<Action>();
            _actionQueueCopy = new List<Action>();
        }

        public static void Patch(PatchContext ptx)
        {
            ptx.GetPattern(_MySessionUpdate).Suffixes.Add(_MySessionUpdatePatch);
        }

        // called in the main loop
        public static void MySessionUpdatePatch()
        {
            _actionQueueCopy.Clear(); // just to be sure

            // prevent infinite loop (when queuing new action inside a queued action)
            while (_actionQueue.TryDequeue(out var action))
            {
                _actionQueueCopy.Add(action);
            }

            foreach (var action in _actionQueueCopy)
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            _actionQueueCopy.Clear();
        }

        public static Task MoveToGameLoop(CancellationToken canceller)
        {
            canceller.ThrowIfCancellationRequested();

            var taskSource = new TaskCompletionSource<byte>();

            _actionQueue.Enqueue(() =>
            {
                try
                {
                    canceller.ThrowIfCancellationRequested();
                    taskSource.TrySetResult(0);
                }
                catch (Exception e)
                {
                    taskSource.SetException(e);
                }
            });

            return taskSource.Task;
        }
    }
}