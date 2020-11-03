﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NLog;
using Profiler.Core;

namespace Profiler.Basics
{
    /// <summary>
    /// Implement basic features of a profiler that makes use of ProfilerEntry.
    /// </summary>
    /// <remarks>You can use ProfilerPatch without this class.</remarks>
    public abstract class BaseProfiler<K> : IProfiler, IDisposable
    {
        // Holds onto ProfileResults until processed.
        readonly ConcurrentQueue<ProfilerResult> _queuedProfilerResults;

        /// <summary>
        /// Cancels processing the ProfilerResult queue.
        /// </summary>
        readonly CancellationTokenSource _queueCanceller;

        // Thread-safe dictionary of ProfilerEntry with an arbitrary type of keys.
        readonly ConcurrentDictionary<K, ProfilerEntry> _profilerEntries;

        // Cached function to unpool (or create) a new ProfilerEntity instance.
        readonly Func<K, ProfilerEntry> _makeProfilerEntity;

        bool _disposed;

        protected BaseProfiler()
        {
            _queuedProfilerResults = new ConcurrentQueue<ProfilerResult>();
            _queueCanceller = new CancellationTokenSource();
            _profilerEntries = new ConcurrentDictionary<K, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        /// <inheritdoc/>
        void IProfiler.OnProfileComplete(in ProfilerResult profilerResult)
        {
            _queuedProfilerResults.Enqueue(profilerResult);
        }

        /// <summary>
        /// Start a thread to process ProfilerResults that are queued by ProfilerPatch.
        /// </summary>
        public void StartProcessQueue()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            ThreadPool.QueueUserWorkItem(_ => ProcessQueue());
        }

        void ProcessQueue()
        {
            var queueCancellerToken = _queueCanceller.Token;
            while (!_queueCanceller.IsCancellationRequested)
            {
                while (_queuedProfilerResults.TryDequeue(out var profilerResult))
                {
                    OnProfilerResultDequeued(profilerResult);
                }

                try
                {
                    // wait for the next interval, or throws if cancelled
                    queueCancellerToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(0.1f));
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        void OnProfilerResultDequeued(in ProfilerResult profilerResult)
        {
            try
            {
                if (TryAccept(profilerResult, out var key))
                {
                    var profilerEntry = _profilerEntries.GetOrAdd(key, _makeProfilerEntity);
                    profilerEntry.Add(profilerResult);
                }
            }
            catch (Exception e)
            {
                // catches exceptions in `TryAccept()`.
                LogManager.GetLogger(GetType().FullName).Error(e);
            }
        }

        /// <summary>
        /// Make a key object using a ProfilerResult sent from ProfilerPatch, or ignore by returning false.
        /// </summary>
        /// <remarks>When accepted, the key object will be registered and will affect this profiler's final result.</remarks>
        /// <param name="profilerResult">Profiling result of a method invocation sent from ProfilerPatch.</param>
        /// <param name="key">Key object to be registered to this profiler if accepted.</param>
        /// <returns>True if given ProfilerResult is accepted, otherwise false.</returns>
        abstract protected bool TryAccept(ProfilerResult profilerResult, out K key);

        /// <summary>
        /// Generate a key-value-pair collection of the key objects and ProfilerEntries.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<(K Key, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(p => (p.Key, p.Value));
        }

        /// <summary>
        /// Removes ProfilerEntity paired with given key object.
        /// </summary>
        /// <param name="key">Key object to remove its associated ProfilerEntity in this profiler.</param>
        protected void RemoveEntry(K key)
        {
            _profilerEntries.Remove(key);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            _disposed = true;
            _queueCanceller.Cancel();
            _queueCanceller.Dispose();

            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}