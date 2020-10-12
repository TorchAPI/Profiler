using System.Threading;
using Profiler.Core;
using Profiler.Util;

namespace Profiler.Interactive
{
    public sealed class ProfilerEntry
    {
        long _totalOffThreadTimeMs;

        ProfilerEntry()
        {
        }

        public long TotalMainThreadTimeMs { get; private set; }
        public long TotalOffThreadTimeMs => _totalOffThreadTimeMs;
        public long TotalTimeMs => TotalMainThreadTimeMs + TotalOffThreadTimeMs;

        public void Add(ProfilerResult profilerResult)
        {
            if (profilerResult.IsMainThread)
            {
                // Always called from the main thread, no Interlocked necessary
                TotalMainThreadTimeMs += profilerResult.TimeMs;
            }
            else
            {
                Interlocked.Add(ref _totalOffThreadTimeMs, profilerResult.TimeMs);
            }
        }

        public void Reset()
        {
            TotalMainThreadTimeMs = 0;
            _totalOffThreadTimeMs = 0;
        }

        public sealed class Pool : ObjectPool<ProfilerEntry>
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