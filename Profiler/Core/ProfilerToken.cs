using System;

namespace Profiler.Core
{
    internal readonly struct ProfilerToken
    {
        public readonly object GameEntity;
        public readonly DateTime StartTimestamp;

        public ProfilerToken(object gameEntity, DateTime startTimestamp)
        {
            GameEntity = gameEntity;
            StartTimestamp = startTimestamp;
        }
    }
}