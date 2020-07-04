using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRageMath;

namespace Profiler.Core
{
    public class ProfilerRequest
    {
        public readonly ProfilerRequestType Type;
        public readonly ulong SamplingTicks;
        internal ulong FinalTick;
        private readonly Dictionary<ProfilerEntry, Working> _entries = new Dictionary<ProfilerEntry, Working>();
        public event DelFinished OnFinished;

        public delegate void DelFinished(Result[] results);

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
            public readonly ProfilerEntry Profiler;

            public Working(string name, Vector3D? ps, string desc, ProfilerEntry spe)
            {
                Name = name;
                Position = ps;
                Description = desc;
                Profiler = spe;
            }

            public Result Commit()
            {
                return new Result(Name, Position, Description, Profiler);
            }
        }

        public struct Result
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Vector3D? Position;

            public readonly double MainThreadMsPerTick;
            public readonly double OffThreadMsPerTick;

            public Result(string name, Vector3D? pos, string desc, ProfilerEntry data)
            {
                Name = name;
                Position = pos;
                Description = desc;

                var deltaTicks = ProfilerData.CurrentTick - data.LastResetTick;
                MainThreadMsPerTick = CalculateMsPerTick(deltaTicks, data.MainThreadTime);
                OffThreadMsPerTick = CalculateMsPerTick(deltaTicks, data.OffThreadTime);
            }

            private static double CalculateMsPerTick(ulong deltaTicks, long time)
            {
                return time * 1000.0D / Stopwatch.Frequency / deltaTicks;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Accept(string name, Vector3D? pos, string desc, ProfilerEntry profilerEntry)
        {
            if (_entries.ContainsKey(profilerEntry))
                return;
            profilerEntry.Reset(ProfilerData.CurrentTick);
            _entries.Add(profilerEntry, new Working(name, pos, desc, profilerEntry));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal void Commit()
        {
            var results = new Result[_entries.Count];
            var i = 0;
            foreach (var k in _entries.Values)
                results[i++] = k.Commit();
            _entries.Clear();
            Array.Sort(results, (a, b) => -a.MainThreadMsPerTick.CompareTo(b.MainThreadMsPerTick));
            OnFinished?.Invoke(results);
            OnFinished = null;
        }

        public void AcceptGrid(long id, ProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Grid)
                return;
            var grid = MyEntities.GetEntityById(id) as MyCubeGrid;
            Accept(grid?.DisplayName ?? "Unknown", grid?.PositionComp.WorldAABB.Center, "ID=" + id, spe);
        }

        public void AcceptProgrammableBlock(long id, ProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Scripts)
                return;
            var block = MyEntities.GetEntityById(id) as MyProgrammableBlock;
            Accept($"{block?.CustomName?.ToString() ?? "Unknown"} on {block?.CubeGrid?.DisplayName ?? "Unknown"}", block?.PositionComp.WorldAABB.Center,
                "ID=" + id, spe);
        }

        public void AcceptFaction(long id, ProfilerEntry spe)
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

        public void AcceptPlayer(long id, ProfilerEntry spe)
        {
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

        private const string TypePrefix = "MyObjectBuilder_";

        public void AcceptBlockType(Type type, ProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockType)
                return;
            var name = type.Name;
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            Accept(name, null, "", spe);
        }

        public void AcceptBlockDefinition(MyCubeBlockDefinition def, ProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockDef)
                return;
            var name = def.Id.TypeId.ToString();
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            name += "/" + def.Id.SubtypeName;
            Accept(name, null, "", spe);
        }

        public const string DistanceFormat = "0.##E+00";
    }

    public enum ProfilerRequestType
    {
        BlockType,
        BlockDef,
        Grid,
        Player,
        Faction,
        Scripts,
        Count
    }
}