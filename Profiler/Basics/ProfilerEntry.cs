using System.Diagnostics;
using Profiler.Core;
using Utils.General;

namespace Profiler.Basics
{
    /// <summary>
    /// Paired with BaseProfiler, provide a summary of computation time per a key object. 
    /// </summary>
    public sealed class ProfilerEntry
    {
        long _rawMainThreadTime;
        long _rawOffThreadTime;

        // Use Pool
        ProfilerEntry()
        {
        }

        /// <summary>
        /// Total main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double MainThreadTime => FromStopwatchTickToMs(_rawMainThreadTime);

        /// <summary>
        /// Total not-main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double OffThreadTime => FromStopwatchTickToMs(_rawOffThreadTime);

        /// <summary>
        /// Total computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double TotalTime => MainThreadTime + OffThreadTime;

        static double FromStopwatchTickToMs(long time)
        {
            return time * 1000.0D / Stopwatch.Frequency;
        }

        internal void Add(in ProfilerResult profilerResult)
        {
            if (profilerResult.IsMainThread)
            {
                _rawMainThreadTime += profilerResult.TotalTick;
            }
            else
            {
                _rawOffThreadTime += profilerResult.TotalTick;
            }
        }

        internal void MergeWith(ProfilerEntry other)
        {
            _rawMainThreadTime += other._rawMainThreadTime;
            _rawOffThreadTime += other._rawOffThreadTime;
        }

        void Reset()
        {
            _rawMainThreadTime = 0;
            _rawOffThreadTime = 0;
        }

        // Pool for ProfilerEntity instances to prevent GC
        internal sealed class Pool : ObjectPool<ProfilerEntry>
        {
            public static readonly Pool Instance = new Pool();

            protected override ProfilerEntry CreateNew()
            {
                var entry = new ProfilerEntry();
                entry.Reset();
                return entry;
            }

            protected override void Reset(ProfilerEntry entity)
            {
                entity.Reset();
            }
        }
    }
}