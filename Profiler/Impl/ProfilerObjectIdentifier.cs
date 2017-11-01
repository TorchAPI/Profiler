using System;
using System.Linq;
using System.Reflection;
using NLog;
using Profiler.Api;
using Sandbox.Game.Entities;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using VRage.Game;
using VRage.Utils;

namespace Profiler.Impl
{
    internal static class ProfilerObjectIdentifier
    {
        /// <summary>
        /// Identifies the given object in a human readable name when profiling
        /// </summary>
        /// <param name="o">object to ID</param>
        /// <returns>ID</returns>
        public static string Identify(object o)
        {
            if (o is MyCubeGrid grid)
            {
                string owners = string.Join(", ", grid.BigOwners.Concat(grid.SmallOwners).Distinct().Select(
                    x => Sync.Players?.TryGetIdentity(x)?.DisplayName ?? $"Identity[{x}]"));
                if (string.IsNullOrWhiteSpace(owners))
                    owners = "unknown";
                return $"{grid.DisplayName ?? ($"{grid.GridSizeEnum} {grid.EntityId}")} owned by [{owners}]";
            }
            if (o is MyDefinitionBase def)
            {
                string typeIdSimple = def.Id.TypeId.ToString().Substring("MyObjectBuilder_".Length);
                string subtype = def.Id.SubtypeName?.Replace(typeIdSimple, "");
                return WithModName(string.IsNullOrWhiteSpace(subtype) ? typeIdSimple : $"{typeIdSimple}::{subtype}",
                    def);
            }
            if (o is string str)
            {
                return !string.IsNullOrWhiteSpace(str) ? str : "unknown string";
            }
            if (o is ProfilerFixedEntry fx)
            {
                string res = fx.ToString();
                return !string.IsNullOrWhiteSpace(res) ? res : "unknown fixed";
            }
            if (o is Type type)
            {
                return WithModName(type.Name, type);
            }
            if (o is MyIdentity identity)
            {
                return
                    $"{identity.DisplayName ?? "unknown identity"} ID={identity.IdentityId} SteamID={MySession.Static?.Players?.TryGetSteamId(identity.IdentityId) ?? 0}";
            }
            if (o is MyCubeBlock block)
            {
                var ownership = MySession.Static?.Players?.TryGetIdentity(block.OwnerId) ??
                                MySession.Static?.Players?.TryGetIdentity(block.BuiltBy);
                return $"{block.GetType().Name} at {block.Min} owned by {Identify(ownership)} on {block.CubeGrid.DisplayName ?? ($"{block.CubeGrid.GridSizeEnum} {block.CubeGrid.EntityId}")}";
            }
            if (o is Assembly asm)
            {
                return WithModName(asm.GetName().Name, asm);
            }
            return WithModName(o?.GetType().Name, o) ?? "unknown";
        }

        private static string WithModName(string baseInfo, object o)
        {
            if (!ProfilerData.DisplayModNames)
                return baseInfo;


            MyModContext ctx = null;
            if (o is MyCubeBlock block)
                ctx = block.BlockDefinition?.Context;
            else if (o is MyDefinitionBase def)
                ctx = def.Context;

            if (ctx == null && MyScriptManager.Static != null)
            {
                Assembly asmToLookup = null;
                if (o is Assembly asm)
                    asmToLookup = asm;
                else if (o is Type type)
                    asmToLookup = type.Assembly;
                else
                    asmToLookup = o?.GetType().Assembly;
                if (asmToLookup != null && string.IsNullOrWhiteSpace(asmToLookup.Location)) // real assemblies have a location.
                {
                    var asmName = asmToLookup.GetName().Name;
                    foreach (var kv in MyScriptManager.Static.ScriptsPerMod)
                    {
                        if (kv.Value.Any(x=>x.String.EndsWith(asmName)))
                        {
                            ctx = kv.Key;
                            break;
                        }
                    }
                }
            }

            if (ctx == MyModContext.BaseGame || ctx == MyModContext.UnknownContext || ctx == null || ctx.IsBaseGame)
                return baseInfo;
            var tag = !string.IsNullOrWhiteSpace(ctx.ModName) ? ctx.ModName : ctx.ModId;
            return $"{baseInfo} (Mod: {tag})";
        }
    }
}
