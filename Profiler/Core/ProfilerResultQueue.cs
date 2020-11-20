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
    public sealed class ProfilerResultQueue
    {
        public static readonly ProfilerResultQueue Instance = new ProfilerResultQueue();

        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        readonly ConcurrentQueue<ProfilerResult> _profilerResults;
        readonly List<IProfiler> _profilers;

        ProfilerResultQueue()
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
        public IDisposable Profile(IProfiler observer)
        {
            AddProfiler(observer);
            return new ActionDisposable(() => RemoveProfiler(observer));
        }

        internal void Enqueue(in ProfilerResult result)
        {
            _profilerResults.Enqueue(result);
        }

        internal void Start(CancellationToken canceller)
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
        void ProcessResult(in ProfilerResult result)
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
        void AddProfiler(IProfiler profiler)
        {
            if (_profilers.Contains(profiler))
            {
                Log.Warn($"Observer already added: {profiler}");
                return;
            }

            _profilers.Add(profiler);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        void RemoveProfiler(IProfiler profiler)
        {
            _profilers.Remove(profiler);
        }
    }
}