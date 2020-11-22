using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using NLog;
using TorchUtils;

namespace Profiler.Core
{
    /// <summary>
    /// Receives ProfilerResults from patched methods & distributes them to multiple observers in a separate thread
    /// </summary>
    public static class ProfilerResultQueue
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        static readonly ConcurrentQueue<ProfilerResult> _profilerResults;
        static readonly List<IProfiler> _profilers;

        static ProfilerResultQueue()
        {
            _profilerResults = new ConcurrentQueue<ProfilerResult>();
            _profilers = new List<IProfiler>();
        }

        /// <summary>
        /// Add an profiler and, when the returned IDisposable object is disposed, remove the profiler from the profiler.
        /// </summary>
        /// <param name="observer">Observer to add/remove.</param>
        /// <returns>IDisposable object that, when disposed, removes the profiler from the profiler.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static IDisposable Profile(IProfiler observer)
        {
            AddProfiler(observer);
            return new ActionDisposable(() => RemoveProfiler(observer));
        }

        internal static void Enqueue(in ProfilerResult result)
        {
            _profilerResults.Enqueue(result);
        }

        internal static void Start(CancellationToken canceller)
        {
            while (!canceller.IsCancellationRequested)
            {
                while (_profilerResults.TryDequeue(out var result))
                {
                    ProcessResult(result);
                }

                try
                {
                    canceller.WaitHandle.WaitOne(TimeSpan.FromSeconds(0.1f));
                }
                catch
                {
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static void ProcessResult(in ProfilerResult result)
        {
            foreach (var profiler in _profilers)
            {
                try
                {
                    profiler.ReceiveProfilerResult(result);
                }
                catch (Exception e)
                {
                    Log.Error($"{profiler}: {e.Message}");
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static void AddProfiler(IProfiler profiler)
        {
            if (_profilers.Contains(profiler))
            {
                Log.Warn($"Observer already added: {profiler}");
                return;
            }

            _profilers.Add(profiler);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        static void RemoveProfiler(IProfiler profiler)
        {
            _profilers.Remove(profiler);
        }
    }
}