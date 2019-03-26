using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows.Media.Media3D;
using Havok;
using NLog;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Engine.Physics;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Blocks;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.World;
using Torch.Utils;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRageMath;
using VRageMath.Spatial;

namespace Profiler.Core
{
    /// <summary>
    /// Class that stores all the timing associated with the profiler.  Use <see cref="ProfilerManager"/> for observable views into this data.
    /// </summary>
    internal class ProfilerData
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        static ProfilerData()
        {
            MyEntities.OnEntityRemove += (x) =>
            {
                if (x == null) return;
                PerfGrid.Remove(x.EntityId);
                PerfScript.Remove(x.EntityId);
            };
        }

        #region Msil Method Handles

        internal static readonly MethodInfo GetEntityProfiler = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(EntityEntry));

        internal static readonly MethodInfo GetGridSystemProfiler = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(GridSystemEntry));

        internal static readonly MethodInfo GetEntityComponentProfiler = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(EntityComponentEntry));

        internal static readonly MethodInfo GetSessionComponentProfiler = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(SessionComponentEntry));

        internal static readonly MethodInfo DoTick = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(Tick));
        #endregion

        private static readonly ConcurrentDictionary<Type, SlimProfilerEntry> PerfBlockType = new ConcurrentDictionary<Type, SlimProfilerEntry>();

        private static readonly ConcurrentDictionary<MyCubeBlockDefinition, SlimProfilerEntry> PerfBlockDef =
            new ConcurrentDictionary<MyCubeBlockDefinition, SlimProfilerEntry>();

        private static readonly ConcurrentDictionary<long, SlimProfilerEntry> PerfGrid = new ConcurrentDictionary<long, SlimProfilerEntry>();
        private static readonly ConcurrentDictionary<long, SlimProfilerEntry> PerfPlayer = new ConcurrentDictionary<long, SlimProfilerEntry>();
        private static readonly ConcurrentDictionary<long, SlimProfilerEntry> PerfFaction = new ConcurrentDictionary<long, SlimProfilerEntry>();
        private static readonly ConcurrentDictionary<long, SlimProfilerEntry> PerfScript = new ConcurrentDictionary<long, SlimProfilerEntry>();

        private static readonly ConcurrentDictionary<MyModContext, SlimProfilerEntry> PerfMod = new ConcurrentDictionary<MyModContext, SlimProfilerEntry>();

        private static readonly ConcurrentDictionary<Type, SlimProfilerEntry> PerfSessionComponent = new ConcurrentDictionary<Type, SlimProfilerEntry>();

        private static readonly ConcurrentDictionary<BoundingBoxD, SlimProfilerEntry> PerfPhysicsClusters =
            new ConcurrentDictionary<BoundingBoxD, SlimProfilerEntry>();

        #region Entry Access

        private static bool AcceptEntity(IMyEntity entity)
        {
            if (_activeProfilers == 0)
                return false;

            var factionMask = _factionMask;
            var playerMask = _playerMask;
            var entityMask = _entityMask;
            var modMask = _modMask;

            if (entityMask.HasValue)
            {
                var tmp = entity;
                var success = false;
                do
                {
                    success |= tmp.EntityId == entityMask.Value;
                    tmp = tmp.Parent;
                } while (tmp != null && !success);

                if (!success)
                    return false;
            }

            do
            {
                switch (entity)
                {
                    case MyCubeBlock block:
                    {
                        if (playerMask.HasValue && playerMask.Value != block.BuiltBy)
                            return false;
                        // ReSharper disable once InvertIf
                        if (factionMask.HasValue)
                        {
                            var faction = MySession.Static.Factions.TryGetPlayerFaction(block.BuiltBy);
                            if ((faction?.FactionId ?? 0) != factionMask.Value)
                                return false;
                        }

                        return modMask == null || Equals(block.BlockDefinition?.Context, modMask);
                    }
                    case MyCubeGrid grid:
                    {
                        if (playerMask.HasValue)
                        {
                            var success = playerMask.Value == 0 && grid.BigOwners.Count == 0;
                            foreach (var owner in grid.BigOwners)
                            {
                                success |= owner == playerMask.Value;
                                if (success)
                                    break;
                            }

                            if (!success)
                                return false;
                        }

                        // ReSharper disable once InvertIf
                        if (factionMask.HasValue)
                        {
                            var success = factionMask.Value == 0 && grid.BigOwners.Count == 0;
                            foreach (var owner in grid.BigOwners)
                            {
                                var faction = MySession.Static.Factions.TryGetPlayerFaction(owner);
                                success |= (faction?.FactionId ?? 0) == factionMask.Value;
                                if (success)
                                    break;
                            }

                            if (!success)
                                return false;
                        }

                        return true;
                    }
                }

                entity = entity.Parent;
            } while (entity != null);

            return false;
        }

        public static void EntityEntry(IMyEntity entity, ref MultiProfilerEntry mpe)
        {
            if (!AcceptEntity(entity))
                return;

            do
            {
                switch (entity)
                {
                    case MyCubeBlock block:
                    {
                        if (block.BlockDefinition != null && (_activeProfilersByType[(int) ProfilerRequestType.BlockDef] > 0 ||
                                                              _activeProfilersByType[(int) ProfilerRequestType.BlockType] > 0))
                        {
                            var def = block.BlockDefinition.Id;
                            mpe.Add(PerfBlockType.GetOrAdd(def.TypeId, DelMakeBlockType));
                            mpe.Add(PerfBlockDef.GetOrAdd(block.BlockDefinition, DelMakeBlockDefinition));
                        }

                        if (block.CubeGrid != null && _activeProfilersByType[(int) ProfilerRequestType.Grid] > 0)
                        {
                            mpe.Add(PerfGrid.GetOrAdd(block.CubeGrid.EntityId, DelMakeGrid));
                        }

                        if (block is MyProgrammableBlock && _activeProfilersByType[(int) ProfilerRequestType.Scripts] > 0)
                            mpe.Add(PerfScript.GetOrAdd(block.EntityId, DelMakeScript));

                        if (_activeProfilersByType[(int) ProfilerRequestType.Players] > 0)
                            mpe.Add(PerfPlayer.GetOrAdd(block.BuiltBy, DelMakePlayer));

                        if (_activeProfilersByType[(int) ProfilerRequestType.Faction] <= 0) return;
                        var faction = MySession.Static.Factions.TryGetPlayerFaction(block.BuiltBy);
                        PerfFaction.GetOrAdd(faction?.FactionId ?? 0, DelMakeFaction);
                        return;
                    }
                    case MyCubeGrid grid:
                    {
                        if (_activeProfilersByType[(int) ProfilerRequestType.Grid] > 0)
                            mpe.Add(PerfGrid.GetOrAdd(grid.EntityId, DelMakeGrid));

                        if (_activeProfilersByType[(int) ProfilerRequestType.Players] <= 0 &&
                            _activeProfilersByType[(int) ProfilerRequestType.Faction] <= 0) return;

                        var addedFaction = false;
                        foreach (var owner in grid.BigOwners)
                        {
                            if (!addedFaction)
                            {
                                var faction = MySession.Static.Factions.TryGetPlayerFaction(owner);
                                if (faction != null)
                                    addedFaction = mpe.Add(PerfFaction.GetOrAdd(faction.FactionId, DelMakeFaction));
                            }

                            mpe.Add(PerfPlayer.GetOrAdd(owner, DelMakePlayer));
                        }

                        if (!addedFaction)
                            mpe.Add(PerfFaction.GetOrAdd(0, DelMakeFaction));
                        return;
                    }
                }

                entity = entity.Parent;
            } while (entity != null);
        }

        // Arguments ordered in this BS way for ease of IL use  (dup)
        private static void GridSystemEntry(object system, IMyEntity grid, ref MultiProfilerEntry mpe)
        {
            if (_activeProfilers == 0)
                return;
            if (grid != null)
                EntityEntry(grid, ref mpe);
        }

        private static void EntityComponentEntry(MyEntityComponentBase component, ref MultiProfilerEntry mpe)
        {
            if (_activeProfilers == 0)
                return;

            if (component.Entity != null)
                EntityEntry(component.Entity, ref mpe);

            // ReSharper disable once InvertIf
            if (_activeProfilersByType[(int) ProfilerRequestType.Mod] > 0)
            {
                var modContext = ModLookupUtils.GetMod(component.GetType()) ?? MyModContext.BaseGame;
                mpe.Add(PerfMod.GetOrAdd(modContext, DelMakeMod));
            }
        }

        private static void SessionComponentEntry(MySessionComponentBase component, ref MultiProfilerEntry mpe)
        {
            if (_activeProfilers == 0)
                return;

            if (_modMask != null && !Equals(component.ModContext, _modMask))
                return;

            if (_activeProfilersByType[(int) ProfilerRequestType.Mod] > 0)
            {
                var modContext = ModLookupUtils.GetMod(component) ?? MyModContext.BaseGame;
                mpe.Add(PerfMod.GetOrAdd(modContext, DelMakeMod));
            }

            if (_activeProfilersByType[(int) ProfilerRequestType.Session] > 0)
                mpe.Add(PerfSessionComponent.GetOrAdd(component.GetType(), DelMakeSessionComponent));
        }

        #region Physics Profiling

#pragma warning disable 649
        [ReflectedMethod(Type = typeof(MyPhysics), Name = "StepWorldsInternal")]
        private static Action<MyPhysics, List<MyTuple<HkWorld, MyTimeSpan>>> _stepWorldsInternal;
#pragma warning restore 649

        [ThreadStatic]
        private static List<MyTuple<HkWorld, MyTimeSpan>> _destWorldProfiling;

        internal static readonly MethodInfo HandlePrefixPhysicsStepWorlds = ReflectionUtils.StaticMethod(typeof(ProfilerData), nameof(PrefixPhysicsStepWorlds));

        // ReSharper disable once InconsistentNaming
        private static bool PrefixPhysicsStepWorlds(MyPhysics __instance)
        {
            if (_activeProfilers == 0 || _activeProfilersByType[(int) ProfilerRequestType.Physics] <= 0)
                return true;

            var dest = _destWorldProfiling ?? (_destWorldProfiling = new List<MyTuple<HkWorld, MyTimeSpan>>());
            _stepWorldsInternal(__instance, dest);
            foreach (var kv in dest)
            foreach (var cluster in MyPhysics.Clusters.GetClusters())
                if (cluster.UserData == kv.Item1)
                {
                    PerfPhysicsClusters.GetOrAdd(cluster.AABB, DelMakePhysicsCluster).FastForward(kv.Item2.TimeSpan, kv.Item1.RigidBodies.Count);
                    break;
                }

            dest.Clear();

            return false;
        }

        #endregion

        #region Factory

        private static readonly Func<long, SlimProfilerEntry> DelMakeGrid = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptGrid(x, res);
            return res;
        };

        private static readonly Func<long, SlimProfilerEntry> DelMakeScript = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptScript(x, res);
            return res;
        };

        private static readonly Func<long, SlimProfilerEntry> DelMakeFaction = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptFaction(x, res);
            return res;
        };

        private static readonly Func<long, SlimProfilerEntry> DelMakePlayer = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptPlayer(x, res);
            return res;
        };

        private static readonly Func<MyModContext, SlimProfilerEntry> DelMakeMod = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptMod(x, res);
            return res;
        };

        private static readonly Func<Type, SlimProfilerEntry> DelMakeSessionComponent = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptSessionComponent(x, res);
            return res;
        };

        private static readonly Func<Type, SlimProfilerEntry> DelMakeBlockType = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptBlockType(x, res);
            return res;
        };

        private static readonly Func<MyCubeBlockDefinition, SlimProfilerEntry> DelMakeBlockDefinition = x =>
        {
            var res = new SlimProfilerEntry();
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptBlockDefinition(x, res);
            return res;
        };

        private static readonly Func<BoundingBoxD, SlimProfilerEntry> DelMakePhysicsCluster = x =>
        {
            var res = new SlimProfilerEntry("bodies");
            lock (_requests)
                foreach (var req in _requests)
                    req.AcceptPhysicsCluster(x, res);
            return res;
        };

        #endregion

        #endregion

        #region Request Manager

        private static long? _factionMask;
        private static long? _playerMask;
        private static long? _entityMask;
        private static MyModContext _modMask;

        private static int _activeProfilers = 0;
        private static readonly int[] _activeProfilersByType = new int[(int) ProfilerRequestType.Count];
        private static readonly List<ProfilerRequest> _expiredRequests = new List<ProfilerRequest>();
        private static readonly List<ProfilerRequest> _requests = new List<ProfilerRequest>();
        public static ulong _currentTick;

        public static bool ChangeMask(long? playerMask, long? factionMask, long? entityMask, MyModContext modMask)
        {
            lock (_requests)
            {
                if (_playerMask == playerMask && _factionMask == factionMask && _entityMask == entityMask && modMask == _modMask)
                    return true;

                if (_requests.Count > 0)
                    return false;
                _playerMask = playerMask;
                _factionMask = factionMask;
                _entityMask = entityMask;
                _modMask = modMask;
                return true;
            }
        }

        public static void Submit(ProfilerRequest req)
        {
            req.FinalTick = req.SamplingTicks + _currentTick;
            Log.Info($"Start profiling {req.Type} for {req.SamplingTicks} ticks");
            lock (_requests)
                _requests.Add(req);
            foreach (var kv in PerfBlockDef)
                req.AcceptBlockDefinition(kv.Key, kv.Value);
            foreach (var kv in PerfBlockType)
                req.AcceptBlockType(kv.Key, kv.Value);
            foreach (var kv in PerfFaction)
                req.AcceptFaction(kv.Key, kv.Value);
            foreach (var kv in PerfGrid)
                req.AcceptGrid(kv.Key, kv.Value);
            foreach (var kv in PerfMod)
                req.AcceptMod(kv.Key, kv.Value);
            foreach (var kv in PerfPlayer)
                req.AcceptPlayer(kv.Key, kv.Value);
            foreach (var kv in PerfSessionComponent)
                req.AcceptSessionComponent(kv.Key, kv.Value);
            foreach (var kv in PerfPhysicsClusters)
                req.AcceptPhysicsCluster(kv.Key, kv.Value);
            foreach (var kv in PerfScript)
                req.AcceptScript(kv.Key, kv.Value);

            Interlocked.Increment(ref _activeProfilers);
            Interlocked.Increment(ref _activeProfilersByType[(int) req.Type]);
        }

        private static void Tick()
        {
            _currentTick++;
            lock (_requests)
            {
                foreach (var req in _requests)
                    if (_currentTick >= req.FinalTick)
                    {
                        Log.Info($"Finished profiling {req.Type} for {req.SamplingTicks} ticks");
                        _expiredRequests.Add(req);
                        req.Commit();

                        Interlocked.Decrement(ref _activeProfilers);
                        Interlocked.Decrement(ref _activeProfilersByType[(int) req.Type]);
                    }

                foreach (var kv in _expiredRequests)
                    _requests.Remove(kv);
                _expiredRequests.Clear();
            }
        }
        #endregion
    }
}
