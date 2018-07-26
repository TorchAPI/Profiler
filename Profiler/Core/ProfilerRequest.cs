using System;
using System.Collections.Generic;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage.Game;

namespace Profiler.Core
{
    public class ProfilerRequest
    {
        public readonly ProfilerRequestType Type;
        public readonly ulong SamplingTicks;
        internal ulong FinalTick;
        private readonly List<KeyValuePair<string, SlimProfilerEntry>> _entries = new List<KeyValuePair<string, SlimProfilerEntry>>();
        public event Action<Result[]> OnFinished;

        public ProfilerRequest(ProfilerRequestType type, ulong samplingTicks)
        {
            Type = type;
            SamplingTicks = samplingTicks;
        }

        private void Accept(string name, SlimProfilerEntry entry)
        {
            entry.PushProfiler(ProfilerData._currentTick);
            _entries.Add(new KeyValuePair<string, SlimProfilerEntry>(name, entry));
        }

        public struct Result
        {
            public readonly string Name;
            public readonly double MsPerTick;
            public readonly double HitsPerTick;

            public Result(string name, double msPerTick, double hitsPerTick)
            {
                Name = name;
                MsPerTick = msPerTick;
                HitsPerTick = hitsPerTick;
            }
        }

        internal void Commit()
        {
            var results = new Result[_entries.Count];
            var i = 0;
            foreach (var k in _entries)
            {
                var time = k.Value.PopProfiler(ProfilerData._currentTick, out var hits);
                results[i++] = new Result(k.Key,time, hits);
            }

            _entries.Clear();
            Array.Sort(results, (a, b) => -a.MsPerTick.CompareTo(b.MsPerTick));
            OnFinished?.Invoke(results);
            OnFinished = null;
        }

        public void AcceptGrid(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Grid)
                return;
            var grid = MyEntities.GetEntityById(id) as MyCubeGrid;
            Accept($"{grid?.DisplayName ?? "Unknown"} ({id})", spe);
        }

        public void AcceptFaction(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Faction)
                return;
            if (id == 0)
            {
                Accept("No Faction", spe);
                return;
            }

            var faction = MySession.Static.Factions?.TryGetFactionById(id);
            Accept($"{faction?.Tag ?? "Unknown"} ({id})", spe);
        }

        public void AcceptPlayer(long id, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Players)
                return;
            if (id == 0)
            {
                Accept("Nobody", spe);
                return;
            }

            var identity = MySession.Static.Players.TryGetIdentity(id);
            var faction = MySession.Static.Factions.TryGetPlayerFaction(id);
            var factionDesc = faction != null ? "[" + faction.Tag + "] " : "";
            Accept($"{identity?.DisplayName ?? "Unknown"} {factionDesc}({id})", spe);
        }

        public void AcceptMod(MyModContext mod, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.Mod)
                return;
            Accept(mod.ModName, spe);
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
            if (mod != null)
                name += $" ({mod.ModName})";
            Accept(name, spe);
        }

        public void AcceptBlockType(Type type, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockType)
                return;
            Accept(type.Name, spe);
        }

        public void AcceptBlockDefinition(MyCubeBlockDefinition def, SlimProfilerEntry spe)
        {
            if (Type != ProfilerRequestType.BlockDef)
                return;
            var name = def.Id.TypeId.ToString();
            if (name.StartsWith(TypePrefix))
                name = name.Substring(TypePrefix.Length);
            name += "/" + def.Id.SubtypeName;
            Accept(name, spe);
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
        Count
    }
}