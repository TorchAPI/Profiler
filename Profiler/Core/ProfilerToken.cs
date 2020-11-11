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
        /// Index of the profiled method.
        /// </summary>
        public readonly int MethodIndex;

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
        /// <param name="methodIndex">Index of the profiled method.</param>
        /// <param name="entrypoint">Entrypoint type of the profiled method.</param>
        /// <param name="startTimestamp">Timestamp of when this profiling started.</param>
        public ProfilerToken(IMyEntity gameEntity, int methodIndex, string entrypoint, DateTime startTimestamp)
        {
            GameEntity = gameEntity;
            MethodIndex = methodIndex;
            Entrypoint = entrypoint;
            StartTimestamp = startTimestamp;
        }
    }
}