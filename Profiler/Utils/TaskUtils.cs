using System;
using System.Threading;
using System.Threading.Tasks;

namespace Profiler.Utils
{
    internal static class TaskUtils
    {
        public static Task MoveToThreadPool(CancellationToken canceller = default)
        {
            canceller.ThrowIfCancellationRequested();

            var taskSource = new TaskCompletionSource<byte>();

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    canceller.ThrowIfCancellationRequested();
                    taskSource.SetResult(0);
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