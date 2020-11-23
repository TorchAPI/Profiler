using System;
using System.Collections.Concurrent;
using NLog;
using Profiler.Core;
using TorchUtils;

namespace Profiler.Basics
{
    /// <summary>
    /// Implement basic features of a profiler that makes use of ProfilerEntry.
    /// </summary>
    /// <remarks>You can use ProfilerPatch without this class.</remarks>
    public abstract class BaseProfiler<K> : IProfiler, IDisposable
    {
        // Thread-safe dictionary of ProfilerEntry with an arbitrary type of keys.
        readonly ConcurrentDictionary<K, ProfilerEntry> _profilerEntries;

        // Cached function to unpool (or create) a new ProfilerEntity instance.
        readonly Func<K, ProfilerEntry> _makeProfilerEntity;

        ulong _startFrameCount;
        DateTime _startTime;

        protected BaseProfiler()
        {
            _profilerEntries = new ConcurrentDictionary<K, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        /// <summary>
        /// Mark the beginning of profiling. Must be called once to properly retrieve the profiling data.
        /// </summary>
        public void MarkStart()
        {
            _startFrameCount = VRageUtils.CurrentGameFrameCount;
            _startTime = DateTime.UtcNow;
        }

        /// <inheritdoc/>
        void IProfiler.ReceiveProfilerResult(in ProfilerResult profilerResult)
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
        abstract protected bool TryAccept(in ProfilerResult profilerResult, out K key);

        /// <summary>
        /// Generate a key-value-pair collection of the key objects and ProfilerEntries.
        /// </summary>
        /// <returns></returns>
        public BaseProfilerResult<K> GetResult()
        {
            var totalFrameCount = VRageUtils.CurrentGameFrameCount - _startFrameCount;
            var totalTime = (DateTime.UtcNow - _startTime).TotalMilliseconds;
            return new BaseProfilerResult<K>(totalFrameCount, totalTime, _profilerEntries);
        }

        /// <inheritdoc/>
        public virtual void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
            _profilerEntries.Clear();
        }
    }
}