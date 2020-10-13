using System.Threading;
using Profiler.Core;
using Profiler.Util;

namespace Profiler.Interactive
{
    public sealed class ProfilerEntry
    {
        long _totalMainThreadTimeMs;
        long _totalOffThreadTimeMs;

        ProfilerEntry()
        {
        }

        public long TotalMainThreadTimeMs => _totalMainThreadTimeMs;
        public long TotalOffThreadTimeMs => _totalOffThreadTimeMs;
        public long TotalTimeMs => TotalMainThreadTimeMs + TotalOffThreadTimeMs;

        public void Add(ProfilerResult profilerResult)
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