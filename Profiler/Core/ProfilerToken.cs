using System;

namespace Profiler.Core
{
    /// <summary>
    /// Mark the beginning of a method profiling and be consumed in the end of profiling.
    /// </summary>
    internal readonly struct ProfilerToken
    {
        public readonly object GameEntity;
        public readonly int MethodIndex;
        public readonly ProfilerCategory Category;
        public readonly long StartTick; // in 100 nanoseconds

        internal ProfilerToken(object gameEntity, int methodIndex, ProfilerCategory category)
        {
            GameEntity = gameEntity;
            MethodIndex = methodIndex;
            Category = category;
            StartTick = DateTime.UtcNow.Ticks;
        }
    }
}