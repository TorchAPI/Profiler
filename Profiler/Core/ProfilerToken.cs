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
        public readonly string Category;
        public readonly long StartTick; // in 100 nanoseconds

        public ProfilerToken(object gameEntity, int methodIndex, string category)
        {
            GameEntity = gameEntity;
            MethodIndex = methodIndex;
            Category = category;
            StartTick = DateTime.UtcNow.Ticks;
        }
    }
}