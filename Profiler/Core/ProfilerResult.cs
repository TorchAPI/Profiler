using System;
using Profiler.Util;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Profiler.Core
{
    /// <summary>
    /// Result of profiling a method executed in the game loop.
    /// </summary>
    public readonly struct ProfilerResult
    {
        public readonly object GameEntity;
        public readonly DateTime StartTimestamp;
        public readonly DateTime StopTimestamp;
        public readonly bool IsMainThread;

        internal ProfilerResult(object gameEntity, DateTime startTimestamp, DateTime stopTimestamp, bool isMainThread)
        {
            GameEntity = gameEntity;
            StartTimestamp = startTimestamp;
            StopTimestamp = stopTimestamp;
            IsMainThread = isMainThread;
        }

        public long TimeMs => (long) (StopTimestamp - StartTimestamp).TotalMilliseconds;

        public IMyEntity GetGameEntity()
        {
            switch (GameEntity)
            {
                case MyEntityComponentBase ecs:
                {
                    return ecs.Entity;
                }
                case IMyEntity objEntity:
                {
                    return objEntity;
                }
                default:
                {
                    return null;
                }
            }
        }

        public T GetParentEntityOfType<T>() where T : class, IMyEntity
        {
            return GetGameEntity().GetParentEntityOfType<T>();
        }
    }
}