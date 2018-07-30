using System;
using System.Collections.Generic;
using System.IO;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game;
using VRageMath;

namespace Profiler.Core
{
    public class ProfilerRequest
    {
        public readonly ProfilerRequestType Type;
        public readonly ulong SamplingTicks;
        internal ulong FinalTick;
        private readonly List<Working> _entries = new List<Working>();
        public event DelFinished OnFinished;

        public delegate void DelFinished(bool printByPassCount, Result[] results);
        
        public ProfilerRequest(ProfilerRequestType type, ulong samplingTicks)
        {
            Type = type;
            SamplingTicks = samplingTicks;
        }

        private struct Working
        {
            public readonly string Name;
            public readonly Vector3D? Position;
            public readonly string Description;
            public readonly SlimProfilerEntry Profiler;

            public Working(string name, Vector3D? ps, string desc, SlimProfilerEntry spe)
            {
                Name = name;
                Position = ps;
                Description = desc;
                Profiler = spe;
            }

            public Result Commit()
            {
                var time = Profiler.PopProfiler(ProfilerData._currentTick, out var hits);
                return new Result(Name, Position, Description, time, Profiler.PassUnits ?? "upt", hits);
            }
        }

        public struct Result
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Vector3D? Position;

            public readonly double MsPerTick;
            public readonly string HitsUnit;
            public readonly double HitsPerTick;

            public Result(string name, Vector3D? pos, string desc, double msPerTick, string hitsUnit, double hitsPerTick)
            {
                Name = name;
                Position = pos;
                Description = desc;
                MsPerTick = msPerTick;
                HitsUnit = hitsUnit;
                HitsPerTick = hitsPerTick;
            }
        }

        private void Accept(string name, Vector3D? pos, string desc, SlimProfilerEntry entry)
        {
            entry.PushProfiler(ProfilerData._currentTick);
            _entries.Add(new Working(name, pos, desc, entry));
        }

        internal void Commit()
        {
            var results = new Result[_entries.Count];
            var i = 0;
            foreach (var k in _entries)
                results[i++] = k.Commit();
            var sortByPassCount = _entries.Count > 0 && _entries[0].Profiler.CounterOnly;
            _entries.Clear();
            if (sortByPassCount)
                Array.Sort(results, (a, b) => -a.HitsPerTick.CompareTo(b.HitsPerTick));
            else
                Array.Sort(results, (a, b) => -a.MsPerTick.CompareTo(b.MsPerTick));
            OnFinished?.Invoke(sortByPassCount, results);
            OnFinished = null;
        }

        public void AcceptGrid(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Grid)
                return;
            var grid = MyEntities.GetEntityById(id) as MyCubeGrid;
            Accept(grid?.DisplayName ?? "Unknown", grid?.PositionComp.WorldAABB.Center, "ID=" + id, spe);
        }

        public void AcceptFaction(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Faction)
                return;
            if (id == 0)
            {
                Accept("No Faction", null, null, spe);
                return;
            }

            var faction = MySession.Static.Factions?.TryGetFactionById(id);
            Accept(faction?.Tag ?? "Unknown", null, "ID=" + id, spe);
        }

        public void AcceptPlayer(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Players)
                return;
            if (id == 0)
            {
                Accept("Nobody", null, null, spe);
                return;
            }

            var identity = MySession.Static.Players.TryGetIdentity(id);
            var faction = MySession.Static.Factions.TryGetPlayerFaction(id);
            var factionDesc = faction != null ? "[" + faction.Tag + "]" : "";
            Accept($"{identity?.DisplayName ?? "Unknown"} {factionDesc}", null, "ID=" + id, spe);
        }

        public void AcceptMod(MyModContext mod, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Mod)
                return;
            Accept(mod.ModName ?? mod.ModId ?? mod.ModPath ?? "Unknown Mod", null, "", spe);
        }

        private const string TypePrefix = "MyObjectBuilder_";

        public void AcceptSessionComponent(Type type, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Session)
                return;
            var mod = ModLookupUtils.GetMod(type);
            var name = type.Name;
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            Accept(name, null, "From " + (mod?.ModName ?? "SE"), spe);
        }

        public void AcceptBlockType(Type type, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockType)
                return;
            var name = type.Name;
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            Accept(name, null, "", spe);
        }

        public void AcceptBlockDefinition(MyCubeBlockDefinition def, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockDef)
                return;
            var name = def.Id.TypeId.ToString();
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            name += "/" + def.Id.SubtypeName;
            Accept(name, null, "", spe);
        }

        public void AcceptPhysicsCluster(BoundingBoxD cluster, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Physics)
                return;
            Accept("Cluster", cluster.Center, "Radius=" + cluster.HalfExtents.Length().ToString(DistanceFormat), spe);
        }

        public const string DistanceFormat = "0.##E+00";

        public void AcceptScript(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Scripts)
                return;
            var block = MyEntities.GetEntityById(id) as MyProgrammableBlock;
            Accept($"{block?.CustomName?.ToString() ?? "Unknown"} on {block?.CubeGrid?.DisplayName ?? "Unknown"}", block?.PositionComp.WorldAABB.Center,
                "ID=" + id, spe);
        }
    }

    public enum ProfilerRequestType
    {
        BlockType,
        BlockDef,
        Grid,
        Players,
        Faction,
        Mod,
        Session,
        Scripts,
        Physics,
        Count
    }
}