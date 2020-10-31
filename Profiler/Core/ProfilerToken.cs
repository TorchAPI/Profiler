using System;
using VRage.ModAPI;

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
        public readonly IMyEntity GameEntity;

        /// <summary>
        /// Entrypoint of the profiled method.
        /// </summary>
        public readonly string Entrypoint;

        /// <summary>
        /// Timestamp of when this profiling started.
        /// </summary>
        public readonly DateTime StartTimestamp;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="gameEntity">Game entity responsible for the profiled method.</param>
        /// <param name="entrypoint">Category of the profiled method.</param>
        /// <param name="startTimestamp">Timestamp of when this profiling started.</param>
        public ProfilerToken(IMyEntity gameEntity, string entrypoint, DateTime startTimestamp)
        {
            GameEntity = gameEntity;
            Entrypoint = entrypoint;
            StartTimestamp = startTimestamp;
        }
    }
}