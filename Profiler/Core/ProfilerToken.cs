using System;

namespace Profiler.Core
{
    /// <summary>
    /// Mark the beginning of a method profiling and be consumed in the end of profiling.
    /// </summary>
    internal readonly struct ProfilerToken
    {
        /// <summary>
        /// Game entity responsible for the profiled method.
        /// </summary>
        /// <remarks>
        /// Null if not associated with a specific game entity.
        /// </remarks>
        public readonly object GameEntity;

        /// <summary>
        /// Index of the profiled method.
        /// </summary>
        public readonly int MethodIndex;

        /// <summary>
        /// Category of the profiled method.
        /// </summary>
        public readonly string Category;

        /// <summary>
        /// Timestamp of when this profiling started.
        /// </summary>
        public readonly DateTime StartTimestamp;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="gameEntity">Game entity responsible for the profiled method. Null if not associated with a specific game entity.</param>
        /// <param name="methodIndex">Index of the profiled method.</param>
        /// <param name="category">Category of the profiled method.</param>
        /// <param name="startTimestamp">Timestamp of when this profiling started.</param>
        public ProfilerToken(object gameEntity, int methodIndex, string category, DateTime startTimestamp)
        {
            GameEntity = gameEntity;
            MethodIndex = methodIndex;
            Category = category;
            StartTimestamp = startTimestamp;
        }
    }
}