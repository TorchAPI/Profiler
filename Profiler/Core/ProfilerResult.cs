﻿using System;
using Profiler.Util;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Profiler.Core
{
    /// <summary>
    /// Profiling data of a method invocation in the game loop.
    /// </summary>
    public readonly struct ProfilerResult
    {
        /// <summary>
        /// Game entity responsible for the profiled method.
        /// </summary>
        public readonly IMyEntity GameEntity;

        /// <summary>
        /// Timestamp of when the profiling started for the profiled method.
        /// </summary>
        public readonly DateTime StartTimestamp;

        /// <summary>
        /// Timestamp of when the profiling ended for the profiled method.
        /// </summary>
        public readonly DateTime StopTimestamp;

        /// <summary>
        /// True if the profiled method was executed in the main thread, otherwise false.
        /// </summary>
        public readonly bool IsMainThread;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="gameEntity">Game entity responsible for the profiled method.</param>
        /// <param name="startTimestamp">Timestamp of when the profiling started for the profiled method.</param>
        /// <param name="stopTimestamp">Timestamp of when the profiling ended for the profiled method.</param>
        /// <param name="isMainThread">True if the profiled method was executed in the main thread, otherwise false.</param>
        internal ProfilerResult(IMyEntity gameEntity, DateTime startTimestamp, DateTime stopTimestamp, bool isMainThread)
        {
            GameEntity = gameEntity;
            StartTimestamp = startTimestamp;
            StopTimestamp = stopTimestamp;
            IsMainThread = isMainThread;
        }

        /// <summary>
        /// Time span from the start to the end of profiling in milliseconds.
        /// </summary>
        public long DeltaTimeMs => (long) (StopTimestamp - StartTimestamp).TotalMilliseconds;
    }
}