using System;
using System.Reflection;

namespace Profiler.Core.Patches
{
    // just an alternative value tupple for v4.6
    public sealed class ProfileBeginTokenTarget
    {
        public ProfileBeginTokenTarget(Type type, string method, MethodInfo tokenCreator)
        {
            Type = type;
            Method = method;
            TokenCreator = tokenCreator;
        }

        public Type Type { get; }
        public string Method { get; }
        public MethodInfo TokenCreator { get; }

        public bool Matches(MethodBase method)
        {
            return method.DeclaringType == Type && method.Name == Method;
        }
    }
}