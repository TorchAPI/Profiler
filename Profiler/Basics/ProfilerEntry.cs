using Profiler.Core;
using Profiler.TorchUtils;

namespace Profiler.Basics
{
    /// <summary>
    /// Paired with BaseProfiler, provide a summary of computation time per a key object. 
    /// </summary>
    public sealed class ProfilerEntry
    {
        // https://docs.microsoft.com/en-us/dotnet/api/system.datetime.ticks
        const double TickToTime = 1D / 10000;

        // Use Pool
        ProfilerEntry()
        {
        }

        /// <summary>
        /// Total main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double TotalMainThreadTime { get; private set; }

        /// <summary>
        /// Total not-main-thread computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double TotalOffThreadTime { get; private set; }

        /// <summary>
        /// Total computation time of the game associated with the key object in milliseconds.
        /// </summary>
        public double TotalTime => TotalMainThreadTime + TotalOffThreadTime;

        internal void Add(ProfilerResult profilerResult)
        {
            if (profilerResult.IsMainThread)
            {
                TotalMainThreadTime += profilerResult.TotalTick * TickToTime;
            }
            else
            {
                TotalOffThreadTime += profilerResult.TotalTick * TickToTime;
            }
        }

        void Reset()
        {
            TotalMainThreadTime = 0;
            TotalOffThreadTime = 0;
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