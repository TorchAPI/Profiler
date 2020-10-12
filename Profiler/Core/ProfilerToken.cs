using System;

namespace Profiler.Core
{
    internal readonly struct ProfilerToken
    {
        public readonly object GameEntity;
        public readonly ProfileType ProfileType;
        public readonly DateTime StartTimestamp;

        public ProfilerToken(object gameEntity, ProfileType profileType, DateTime startTimestamp)
        {
            GameEntity = gameEntity;
            ProfileType = profileType;
            StartTimestamp = startTimestamp;
        }
    }
}