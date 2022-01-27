using System;
using System.Collections.Generic;

namespace Profiler.Utils
{
    internal static class CollectionUtils
    {
        public static bool TryGetFirst<T>(this IEnumerable<T> self, Func<T, bool> f, out T foundValue)
        {
            foreach (var t in self)
            {
                if (f(t))
                {
                    foundValue = t;
                    return true;
                }
            }

            foundValue = default;
            return false;
        }

        public static bool TryGetFirst<T>(this IEnumerable<T> self, out T foundValue)
        {
            foreach (var t in self)
            {
                foundValue = t;
                return true;
            }

            foundValue = default;
            return false;
        }

        public static IEnumerable<U> WhereAssignable<T, U>(this IEnumerable<T> self)
        {
            foreach (var t in self)
            {
                if (t is U u)
                {
                    yield return u;
                }
            }
        }

        public static ISet<T> ToSet<T>(this IEnumerable<T> self)
        {
            return new HashSet<T>(self);
        }
    }
}