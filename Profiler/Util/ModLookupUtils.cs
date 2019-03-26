using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Game.Components;

namespace Profiler.Util
{
    public static class ModLookupUtils
    {
        private static readonly ConcurrentDictionary<Assembly, MyModContext> _modByAssembly =
            new ConcurrentDictionary<Assembly, MyModContext>();

        public static MyModContext GetMod(object o)
        {
            switch (o)
            {
                case MyCubeBlock block:
                    return block.BlockDefinition?.Context;
                case MyDefinitionBase def:
                    return def.Context;
                case MySessionComponentBase ses:
                    return ses.ModContext as MyModContext;
            }

            var asm = (o as Assembly) ?? (o as Type)?.Assembly ?? o?.GetType().Assembly;
            return asm != null ? _modByAssembly.GetOrAdd(asm, GetModByAssembly) : null;
        }

        private static MyModContext GetModByAssembly(Assembly asmToLookup)
        {
            if (MyScriptManager.Static == null)
                return null;
            if (asmToLookup == null || !string.IsNullOrWhiteSpace(asmToLookup.Location)) return null;
            
            var asmName = asmToLookup.GetName().Name;
            foreach (var kv in MyScriptManager.Static.ScriptsPerMod)
            foreach (vas asmTest in kv.Value)
            if (asmTest.String.EndsWith(asmName)))
                return kv.Key;
            return null;
        }
    }
}
