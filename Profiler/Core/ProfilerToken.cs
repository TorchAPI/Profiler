using System.Diagnostics;

namespace Profiler.Core
{
    internal readonly struct ProfilerToken
    {
        public readonly ProfilerEntry Entry;
        public readonly long Start;

        public ProfilerToken(ProfilerEntry entry)
        {
            Entry = entry;
            Start = Stopwatch.GetTimestamp();
        }
    }
}