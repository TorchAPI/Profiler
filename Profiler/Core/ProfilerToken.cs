using System;
using System.Diagnostics;

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
        public readonly long StartTick; // depends on Stopwatch.Frequency

        internal ProfilerToken(object gameEntity, int methodIndex, ProfilerCategory category)
        {
            GameEntity = gameEntity;
            MethodIndex = methodIndex;
            Category = category;
            StartTick = Stopwatch.GetTimestamp();
        }

        public override string ToString()
        {
            var method = StringIndexer.Instance.StringAt(MethodIndex);
            return $"{nameof(GameEntity)}: {GameEntity}, Method: {method}, {nameof(Category)}: {Category}, {nameof(StartTick)}: {StartTick}";
        }
    }
}