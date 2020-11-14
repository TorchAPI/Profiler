using System;
using System.Reflection;

namespace Profiler.Util
{
    public static class ReflectionUtils
    {
        public const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        
        public static MethodInfo Method(this Type type, string name, BindingFlags flags)
        {
            return type.GetMethod(name, flags) ?? throw new Exception($"Couldn't find method {name} on {type}");
        }

        public static MethodInfo InstanceMethod(this Type t, string name)
        {
            return Method(t, name, InstanceFlags);
        }

        public static MethodInfo StaticMethod(this Type t, string name)
        {
            return Method(t, name, StaticFlags);
        }
    }
}