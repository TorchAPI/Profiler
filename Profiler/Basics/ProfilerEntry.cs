using System.Threading;
using Profiler.Core;
using Profiler.Util;

namespace Profiler.Basics
{
    /// <summary>
    /// Paired with BaseProfiler, provide a summary of computation time per a key object. 
    /// </summary>
    public sealed class ProfilerEntry
    {
        long _totalMainThreadTimeMs;
        long _totalOffThreadTimeMs;

        // Use Pool
        ProfilerEntry()
        {
        }

        /// <summary>
        /// Total main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public long TotalMainThreadTimeMs => _totalMainThreadTimeMs;

        /// <summary>
        /// Total not-main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public long TotalOffThreadTimeMs => _totalOffThreadTimeMs;

        /// <summary>
        /// Total computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public long TotalTimeMs => TotalMainThreadTimeMs + TotalOffThreadTimeMs;

        internal void Add(ProfilerResult profilerResult)
        {
            if (profilerResult.IsMainThread)
            {
                Interlocked.Add(ref _totalMainThreadTimeMs, profilerResult.DeltaTimeMs);
            }
            else
            {
                Interlocked.Add(ref _totalOffThreadTimeMs, profilerResult.DeltaTimeMs);
            }
        }

        void Reset()
        {
            _totalMainThreadTimeMs = 0;
            _totalOffThreadTimeMs = 0;
        }

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