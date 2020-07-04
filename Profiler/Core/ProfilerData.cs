using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.World;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Profiler.Core
{
    /// <summary>
    /// Class that stores all the timing associated with the profiler.  Use <see cref="ProfilerManager"/> for observable views into this data.
    /// </summary>
    internal class ProfilerData
    {
        static ProfilerData()
        {
            MyEntities.OnEntityRemove += (x) =>
            {
                if (x == null) return;
                PerfGrid.Remove(x.EntityId);
                PerfProgrammableBlock.Remove(x.EntityId);
            };
        }

        #region Msil Method Handles

        internal static readonly MethodInfo GetGenericProfilerToken = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(Start));
        internal static readonly MethodInfo StopProfilerToken = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(StopToken));

        internal static readonly MethodInfo DoTick = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(Tick));

        #endregion

        private static readonly ConcurrentDictionary<Type, ProfilerEntry> PerfBlockType = new ConcurrentDictionary<Type, ProfilerEntry>();

        private static readonly ConcurrentDictionary<MyCubeBlockDefinition, ProfilerEntry> PerfBlockDef =
            new ConcurrentDictionary<MyCubeBlockDefinition, ProfilerEntry>();

        private static readonly ConcurrentDictionary<long, ProfilerEntry> PerfGrid = new ConcurrentDictionary<long, ProfilerEntry>();
        private static readonly ConcurrentDictionary<long, ProfilerEntry> PerfProgrammableBlock = new ConcurrentDictionary<long, ProfilerEntry>();
        private static readonly ConcurrentDictionary<long, ProfilerEntry> PerfPlayer = new ConcurrentDictionary<long, ProfilerEntry>();
        private static readonly ConcurrentDictionary<long, ProfilerEntry> PerfFaction = new ConcurrentDictionary<long, ProfilerEntry>();

        internal static ulong CurrentTick;

        private static ProfilerRequest _active;
        private static long? _factionMask;
        private static long? _playerMask;
        private static long? _gridMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ProfilerToken? StartProgrammableBlock(MyProgrammableBlock block)
        {
            if (_active?.Type != ProfilerRequestType.Scripts || !AcceptBlock(block))
                return null;
            return new ProfilerToken(PerfProgrammableBlock.GetOrAdd(block.EntityId, DelMakeProgrammableBlock));
        }

        public static ProfilerToken? Start(object obj)
        {
            if (_active == null)
                return null;
            var mode = _active.Type;

            IMyEntity entity;
            switch (obj)
            {
                case MyEntityComponentBase ecs:
                    entity = ecs.Entity;
                    break;
                case IMyEntity objEntity:
                    entity = objEntity;
                    break;
                default:
                    return null;
            }

            ProfilerEntry result;
            switch (mode)
            {
                case ProfilerRequestType.BlockType:
                case ProfilerRequestType.BlockDef:
                {
                    var block = FindParent<MyCubeBlock>(entity);
                    if (block == null || !AcceptBlock(block) || block.BlockDefinition == null)
                        return null;
                    result = mode == ProfilerRequestType.BlockDef
                        ? PerfBlockDef.GetOrAdd(block.BlockDefinition, DelMakeBlockDefinition)
                        : PerfBlockType.GetOrAdd(block.GetType(), DelMakeBlockType);
                    break;
                }
                case ProfilerRequestType.Grid:
                {
                    var grid = FindParent<MyCubeGrid>(entity);
                    if (grid == null || !AcceptGrid(grid))
                        return null;
                    result = PerfGrid.GetOrAdd(grid.EntityId, DelMakeGrid);
                    break;
                }
                case ProfilerRequestType.Player:
                {
                    var player = ExtractPlayerIfAccepted(entity);
                    if (!player.HasValue)
                        return null;
                    result = PerfPlayer.GetOrAdd(player.Value, DelMakePlayer);
                    break;
                }
                case ProfilerRequestType.Faction:
                {
                    var player = ExtractPlayerIfAccepted(entity);
                    if (!player.HasValue)
                        return null;
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(player.Value);
                    if (faction == null)
                        return null;
                    result = PerfFaction.GetOrAdd(faction.FactionId, DelMakeFaction);
                    break;
                }
                case ProfilerRequestType.Scripts:
                    // Profiled somewhere else
                    return null;
                case ProfilerRequestType.Count:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new ProfilerToken(result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StopToken(in ProfilerToken? token, bool mainThreadUpdate)
        {
            if (token.HasValue)
                Stop(token.Value.Entry, mainThreadUpdate, token.Value.Start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Stop(ProfilerEntry entry, bool mainThreadUpdate, long startTime)
        {
            if (_active == null || entry == null || startTime == 0)
                return;
            var endTime = Stopwatch.GetTimestamp();
            var dt = endTime - startTime;
            if (mainThreadUpdate)
            {
                // Always called from the main thread, no Interlocked necessary
                entry.MainThreadTime += dt;
                entry.MainThreadUpdates++;
            }
            else
            {
                Interlocked.Add(ref entry.OffThreadTime, dt);
                Interlocked.Increment(ref entry.OffThreadUpdates);
            }
        }

        private static long? ExtractPlayerIfAccepted(IMyEntity entity)
        {
            if (entity is MyCubeGrid grid)
            {
                if (grid.BigOwners.Count == 0 || !AcceptGrid(grid))
                    return null;
                // Use player mask if present instead of the first big owner
                return _playerMask ?? grid.BigOwners[0];
            }

            var block = FindParent<MyCubeBlock>(entity);
            if (block == null || !AcceptBlock(block))
                return null;
            return block.BuiltBy;
        }

        private static bool AcceptBlock(MyCubeBlock block)
        {
            if (_gridMask.HasValue && _gridMask != block.Parent.EntityId)
                return false;
            if (_playerMask.HasValue && block.BuiltBy != _playerMask)
                return false;
            if (_factionMask.HasValue)
            {
                var faction = MySession.Static.Factions.TryGetPlayerFaction(block.BuiltBy);
                if (faction == null || faction.FactionId != _factionMask)
                    return false;
            }

            return true;
        }

        private static bool AcceptGrid(MyCubeGrid grid)
        {
            if (_gridMask.HasValue && _gridMask != grid.EntityId)
                return false;
            if (_playerMask.HasValue && !grid.BigOwners.Contains(_playerMask.Value))
                return false;
            if (_factionMask.HasValue)
            {
                var good = false;
                foreach (var owner in grid.BigOwners)
                {
                    var faction = MySession.Static.Factions.TryGetPlayerFaction(owner);
                    if (faction != null && faction.FactionId == _factionMask)
                    {
                        good = true;
                        break;
                    }
                }

                if (!good)
                    return false;
            }

            return true;
        }

        private static T FindParent<T>(IMyEntity ent) where T : class, IMyEntity
        {
            while (ent != null)
            {
                if (ent is T match)
                    return match;
                ent = ent.Parent;
            }

            return null;
        }

        private static readonly Func<long, ProfilerEntry> DelMakeGrid = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptGrid(x, res);
            return res;
        };

        private static readonly Func<long, ProfilerEntry> DelMakeFaction = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptFaction(x, res);
            return res;
        };

        private static readonly Func<long, ProfilerEntry> DelMakePlayer = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptPlayer(x, res);
            return res;
        };


        private static readonly Func<Type, ProfilerEntry> DelMakeBlockType = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptBlockType(x, res);
            return res;
        };

        private static readonly Func<MyCubeBlockDefinition, ProfilerEntry> DelMakeBlockDefinition = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptBlockDefinition(x, res);
            return res;
        };

        private static readonly Func<long, ProfilerEntry> DelMakeProgrammableBlock = x =>
        {
            var res = new ProfilerEntry();
            _active?.AcceptProgrammableBlock(x, res);
            return res;
        };

        public static bool Submit(ProfilerRequest req, long? gridMask, long? playerMask, long? factionMask)
        {
            if (Interlocked.CompareExchange(ref _active, req, null) != null)
                return false;
            _gridMask = gridMask;
            _playerMask = playerMask;
            _factionMask = factionMask;
            req.FinalTick = req.SamplingTicks + CurrentTick;
            switch (req.Type)
            {
                case ProfilerRequestType.BlockType:
                    foreach (var (k, v) in PerfBlockType)
                        req.AcceptBlockType(k, v);
                    break;
                case ProfilerRequestType.BlockDef:
                    foreach (var (k, v) in PerfBlockDef)
                        req.AcceptBlockDefinition(k, v);
                    break;
                case ProfilerRequestType.Grid:
                    foreach (var (k, v) in PerfGrid)
                        req.AcceptGrid(k, v);
                    break;
                case ProfilerRequestType.Player:
                    foreach (var (k, v) in PerfPlayer)
                        req.AcceptPlayer(k, v);
                    break;
                case ProfilerRequestType.Faction:
                    foreach (var (k, v) in PerfFaction)
                        req.AcceptFaction(k, v);
                    break;
                case ProfilerRequestType.Scripts:
                    foreach (var (k, v) in PerfProgrammableBlock)
                        req.AcceptProgrammableBlock(k, v);
                    break;
                case ProfilerRequestType.Count:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return true;
        }

        private static void Tick()
        {
            CurrentTick++;
            if (_active != null && _active.FinalTick <= CurrentTick)
            {
                var req = Interlocked.Exchange(ref _active, null);
                req?.Commit();
            }
        }
    }
}