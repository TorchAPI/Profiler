using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Profiler.Utils
{
    public static class ReflectionUtils
    {
        public const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        public const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        public static MethodInfo GetMethod(this Type type, string name, BindingFlags flags)
        {
            return type.GetMethod(name, flags) ?? throw new Exception($"Couldn't find method {name} on {type}");
        }

        public static MethodInfo[] GetMethods(this Type type, string name, BindingFlags flags)
        {
            return type.GetMethods(flags).Where(m => m.Name == name).ToArray();
        }

        public static MethodInfo GetInstanceMethod(this Type t, string name)
        {
            return GetMethod(t, name, InstanceFlags);
        }

        public static MethodInfo GetStaticMethod(this Type t, string name)
        {
            return GetMethod(t, name, StaticFlags);
        }

        static Type[] GetTypesSafe(this Assembly self)
        {
            try
            {
                return self.GetTypes();
            }
            catch (ReflectionTypeLoadException)
            {
                return new Type[0];
            }
        }

        // Type.GetType() but you don't have to specify the assembly qualified name (=durable to game updates)
        public static Type GetTypeByName(string fullName)
        {
            var typeName = fullName.Split('.').Last();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var type in assembly.GetTypesSafe())
            {
                if (type.Name != typeName) continue;
                if (!type.FullName?.Contains(fullName) ?? true) continue;

                return type;
            }

            throw new TypeInitializationException(fullName, new NullReferenceException());
        }

        public static Type[] GetDerivedTypes(this Type self)
        {
            var derivedTypes = new List<Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            foreach (var type in assembly.GetTypesSafe())
            {
                var isDerived = self.IsAssignableFrom(type);
                if (isDerived)
                {
                    derivedTypes.Add(type);
                }
            }

            return derivedTypes.ToArray();
        }
    }
}