using System;

namespace Profiler.Core
{
    /// <summary>
    /// Profiling data of a method invocation in the game loop.
    /// </summary>
    public readonly struct ProfilerResult
    {
        /// <summary>
        /// Index of the profiled method.
        /// </summary>
        readonly int _methodIndex;

        /// <summary>
        /// Game entity responsible for the profiled method.
        /// </summary>
        /// <remarks>
        /// Null if not associated with a specific game entity.
        /// </remarks>
        public readonly object GameEntity;

        /// <summary>
        /// Category of the profiled method.
        /// </summary>
        public readonly ProfilerCategory Category;

        /// <summary>
        /// Time spent during profiling in 100 nanoseconds.
        /// </summary>
        public readonly long TotalTick;

        /// <summary>
        /// True if the profiled method was executed in the main thread, otherwise false.
        /// </summary>
        public readonly bool IsMainThread;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="token">Token which stores the profiled entity's metadata and timestamp.</param>
        /// <param name="isMainThread">True if the profiled method was executed in the main thread, otherwise false.</param>
        internal ProfilerResult(ProfilerToken token, bool isMainThread)
        {
            _methodIndex = token.MethodIndex;
            GameEntity = token.GameEntity;
            Category = token.Category;
            TotalTick = DateTime.UtcNow.Ticks - token.StartTick;
            IsMainThread = isMainThread;
        }

        /// <summary>
        /// Name of the profiled method.
        /// </summary>
        public string MethodName => StringIndexer.Instance.StringAt(_methodIndex);
    }
}