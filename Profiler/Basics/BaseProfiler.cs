using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NLog;
using Profiler.Core;
using Utils.Torch;

namespace Profiler.Basics
{
    /// <summary>
    /// Smart baseclass of IProfiler to support the most common use case of ProfilerResultQueue.
    /// Stacks up profiler results and retrieves the total profiled time of each "key" object,
    /// defined by the implementation of abstract method `TryAccept()`.
    /// </summary>
    public abstract class BaseProfiler<K> : IProfiler, IDisposable
    {
        // Thread-safe dictionary of ProfilerEntry with an arbitrary type of keys.
        readonly ConcurrentDictionary<K, ProfilerEntry> _profilerEntries;

        // Cached function to unpool (or create) a new ProfilerEntity instance.
        readonly Func<K, ProfilerEntry> _makeProfilerEntity;

        // temporary storage of keys
        readonly List<K> _tmpKeys;

        ulong _startFrameCount;
        DateTime _startTime;
        bool _ended;

        protected BaseProfiler()
        {
            _profilerEntries = new ConcurrentDictionary<K, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
            _tmpKeys = new List<K>();
        }

        /// <summary>
        /// Mark the beginning of profiling. Must be called once to properly retrieve the profiling data.
        /// </summary>
        public virtual void MarkStart()
        {
            _startFrameCount = VRageUtils.CurrentGameFrameCount;
            _startTime = DateTime.UtcNow;
        }

        /// <summary>
        /// Mark the end of profiling. Can be called before extracting the profiling result.
        /// </summary>
        public virtual void MarkEnd()
        {
            _ended = true;
        }

        /// <inheritdoc/>
        void IProfiler.ReceiveProfilerResult(in ProfilerResult profilerResult)
        {
            if (_ended) return;

            try
            {
                Accept(profilerResult, _tmpKeys);
                foreach (var key in _tmpKeys)
                {
                    var profilerEntry = _profilerEntries.GetOrAdd(key, _makeProfilerEntity);
                    profilerEntry.Add(profilerResult);
                }

                _tmpKeys.Clear();
            }
            catch (Exception e)
            {
                // catches exceptions in `TryAccept()`.
                LogManager.GetLogger(GetType().FullName).Error(e);
            }
        }

        /// <summary>
        /// Make a "key" object or ignore the profiler result.
        /// </summary>
        /// <remarks>
        /// Called from a single worker thread.
        /// </remarks>
        /// <param name="profilerResult">Profiling result of a method invocation sent from ProfilerPatch.</param>
        /// <param name="acceptedKeys">Keys to be registered to this profiler.</param>
        protected abstract void Accept(in ProfilerResult profilerResult, ICollection<K> acceptedKeys);

        /// <summary>
        /// Generate a key-value-pair collection of the key objects and ProfilerEntries.
        /// </summary>
        /// <returns></returns>
        public BaseProfilerResult<K> GetResult()
        {
            var totalFrameCount = VRageUtils.CurrentGameFrameCount - _startFrameCount;
            var totalTime = (DateTime.UtcNow - _startTime).TotalMilliseconds;

            // copy here so that we wont have concurrency issues down the road
            // https://stackoverflow.com/questions/11692389
            var entries = _profilerEntries.ToArray().ToDictionary(p => p.Key, p => p.Value);
            return new BaseProfilerResult<K>(totalFrameCount, totalTime, entries);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
            _profilerEntries.Clear();
        }
    }
}