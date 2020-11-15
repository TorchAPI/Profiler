using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Profiler.Util;
using VRage.Game.Components;

namespace Profiler.Core.Patches
{
    public static class MySessionComponentBase_
    {
        public static readonly Type Type = typeof(MySessionComponentBase);
        static readonly Type[] DerivedTypes = Type.GetDerivedTypes();

        public static IEnumerable<MethodInfo> DerivedInstanceMethods(string methodName)
        {
            return DerivedTypes.Select(t => t.InstanceMethod(methodName));
        }
    }
}