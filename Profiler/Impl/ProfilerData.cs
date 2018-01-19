using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml.Serialization;
using NLog;
using ParallelTasks;
using Profiler.Api;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Multiplayer;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Torch.Utils;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Profiler.Impl
{
    /// <summary>
    /// Class that stores all the timing associated with the profiler.  Use <see cref="ProfilerManager"/> for observable views into this data.
    /// </summary>
    internal class ProfilerData
    {
        private static readonly Logger _log = LogManager.GetCurrentClassLogger();

        // Don't assign directly.  Use ProfilerManager.
        internal static bool ProfileGridsUpdate = true;

        internal static bool ProfileBlocksUpdate = false;
        internal static bool ProfileBlocksIndividually = false;
        internal static bool ProfileEntityComponentsUpdate = false;
        internal static bool ProfileGridSystemUpdates = false;
        internal static bool ProfileSessionComponentsUpdate = false;
        internal static bool ProfileSingleMethods = false;
        internal static bool ProfileVoxels = true;
        internal static bool ProfileCharacterEntities = true;
        internal static bool DisplayLoadPercentage = false;
        internal static bool DisplayModNames = true;

        internal static bool AnonymousProfilingDumps = false;

        // Doesn't update properly during runtime so not exposed.
        private static bool ProfileBlocksUpdateByOwner = true;

        #region Msil Method Handles

        private const BindingFlags INSTANCE_FLAGS =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static MethodInfo Method(Type type, string name, BindingFlags flags)
        {
            return type.GetMethod(name, flags) ?? throw new Exception($"Couldn't find method {name} on {type}");
        }

        internal static readonly MethodInfo ProfilerEntryStart =
            Method(typeof(SlimProfilerEntry), nameof(SlimProfilerEntry.Start), INSTANCE_FLAGS);

        internal static readonly MethodInfo ProfilerEntryStop =
            Method(typeof(SlimProfilerEntry), nameof(SlimProfilerEntry.Stop), INSTANCE_FLAGS);

        internal static readonly MethodInfo GetEntityProfiler =
            Method(typeof(ProfilerData), nameof(EntityEntry), STATIC_FLAGS);

        internal static readonly MethodInfo GetGridSystemProfiler =
            Method(typeof(ProfilerData), nameof(GridSystemEntry), STATIC_FLAGS);

        internal static readonly MethodInfo GetEntityComponentProfiler =
            Method(typeof(ProfilerData), nameof(EntityComponentEntry), STATIC_FLAGS);

        internal static readonly MethodInfo GetSessionComponentProfiler =
            Method(typeof(ProfilerData), nameof(SessionComponentEntry), STATIC_FLAGS);

        internal static readonly MethodInfo GetSlim =
            Method(typeof(FatProfilerEntry), nameof(FatProfilerEntry.GetSlim), INSTANCE_FLAGS);

        internal static readonly MethodInfo GetFat =
            Method(typeof(FatProfilerEntry), nameof(FatProfilerEntry.GetFat), INSTANCE_FLAGS);

        internal static readonly FieldInfo FieldProfileSingleMethods =
            typeof(ProfilerData).GetField(nameof(ProfileSingleMethods), STATIC_FLAGS) ??
            throw new Exception($"Couldn't find profile single methods setting");

        internal static readonly MethodInfo DoRotateEntries =
            Method(typeof(ProfilerData), nameof(RotateEntries), STATIC_FLAGS);

        #endregion

        #region View Models

        internal static ProfilerEntryViewModel BindView(ProfilerEntryViewModel cache = null)
        {
            if (cache != null)
                return cache;
            using (_boundViewModelsLock.WriteUsing())
                _boundViewModels.Add(GCHandle.Alloc(cache = new ProfilerEntryViewModel(), GCHandleType.Weak));
            return cache;
        }

        private static readonly ReaderWriterLockSlim _boundViewModelsLock = new ReaderWriterLockSlim();

        private static readonly List<GCHandle> _boundViewModels =
            new List<GCHandle>();

        #endregion

        #region Rotation

        public const int RotateInterval = 300;
        private static int _rotateIntervalCounter = RotateInterval - 1;

        private static uint _tickId = 0;

        private static void RotateEntries()
        {
            _tickId++;

            bool refreshedModels = false;
            if (Interlocked.Exchange(ref _forceFullPropertyUpdate, 0) != 0)
            {
                RefreshModels();
                refreshedModels = true;
            }

            // we remove == write
            using (_profilingEntriesAllLock.WriteUsing())
            {
                var i = _rotateIntervalCounter;
                while (i < _profilingEntriesAll.Count)
                {
                    var entry = _profilingEntriesAll[i];
                    var profiler = entry.Value.Target as SlimProfilerEntry;
                    if (profiler != null && (!entry.Key.HasValue || ShouldProfile(entry.Key.Value.Target)))
                    {
                        profiler.Rotate(_tickId);
                        i += RotateInterval;
                    }
                    else
                    {
                        // Value type; if we free we MUST remove
                        if (entry.Key.HasValue && entry.Key.Value.IsAllocated)
                            entry.Key.Value.Free();
                        if (entry.Value.IsAllocated)
                            entry.Value.Free();
                        _profilingEntriesAll.RemoveAtFast(i);
                    }
                }
            }

            if (_rotateIntervalCounter++ > RotateInterval)
            {
                _rotateIntervalCounter = 0;
                if (!refreshedModels)
                    RefreshModels();
            }
        }

        private class AsyncViewModelUpdate : IPrioritizedWork
        {
            private volatile int _offset;
            private int _workers = 0;

            /// <inheritdoc cref="IWork.Options"/>
            public WorkOptions Options { get; }

            public AsyncViewModelUpdate()
            {
                Options = new WorkOptions
                {
                    MaximumThreads = int.MaxValue
                };
                _offset = 0;
            }

            internal void Reset()
            {
                _offset = 0;
                _workers = 0;
            }

            public void DoWork(WorkData workData = null)
            {
                var watch = new Stopwatch();
                watch.Restart();
                Interlocked.Increment(ref _workers);
                while (true)
                {
                    int offset;
                    using (_boundViewModelsLock.ReadUsing())
                    {
                        offset = _offset++;
                        if (offset >= _boundViewModels.Count)
                            break;
                    }
                    var handle = _boundViewModels[offset];
                    var model = handle.Target as ProfilerEntryViewModel;
                    if (model != null && !model.Update())
                    {
                        handle.Free();
                        _boundViewModels[offset] = handle;
                    }
                }
                // Last worker leaving
                if (Interlocked.Decrement(ref _workers) == 0)
                {
                    using (_boundViewModelsLock.UpgradableReadUsing())
                    {
                        var i = 0;
                        while (i < _boundViewModels.Count)
                        {
                            var handle = _boundViewModels[i];
                            if (!handle.IsAllocated ||
                                !(handle.Target is ProfilerEntryViewModel))
                                using (_boundViewModelsLock.WriteUsing())
                                {
                                    if (handle.IsAllocated)
                                        handle.Free();
                                    // _boundViewModels[i] = handle;
                                    _boundViewModels.RemoveAtFast(i);
                                }
                            else
                                i++;
                        }
                    }
                    _log.Trace($"Updated view models in {watch.Elapsed}");
                }
            }

            public WorkPriority Priority => WorkPriority.VeryLow;
        }

        private static readonly AsyncViewModelUpdate _updateViewModelWork = new AsyncViewModelUpdate();
        private static Task? _updateViewModelTask = null;

        private static void RefreshModels()
        {
            _updateViewModelTask?.Wait();
            _updateViewModelWork.Reset();
            _updateViewModelTask = Parallel.Start(_updateViewModelWork);
        }

        private static int _forceFullPropertyUpdate = 0;

        internal static void ForcePropertyUpdate()
        {
            _forceFullPropertyUpdate = 1;
        }

        #endregion

        #region Internal Access

        private static readonly ReaderWriterLockSlim _profilingEntriesAllLock = new ReaderWriterLockSlim();

        private static readonly List<KeyValuePair<GCHandle?, GCHandle>> _profilingEntriesAll =
            new List<KeyValuePair<GCHandle?, GCHandle>>();

        private static readonly FatProfilerEntry[] _fixed;

        internal static FatProfilerEntry FixedProfiler(ProfilerFixedEntry item)
        {
            return _fixed[(int) item] ?? throw new InvalidOperationException($"Fixed profiler {item} doesn't exist");
        }

        static ProfilerData()
        {
            _fixed = new FatProfilerEntry[(int) ProfilerFixedEntry.Count];
            // add to end is "read"
            using (_profilingEntriesAllLock.ReadUsing())
                for (var i = 0; i < _fixed.Length; i++)
                {
                    _fixed[i] = new FatProfilerEntry();
                    _profilingEntriesAll.Add(
                        new KeyValuePair<GCHandle?, GCHandle>(null, GCHandle.Alloc(_fixed[i], GCHandleType.Weak)));
                }
        }

        // ReSharper disable ConvertToConstant.Local
        // Don't make these constants.  We need to keep the reference alive and the same for the weak table.
        private static readonly string _blocksKey = "Blocks";

        private static readonly string _systemsKey = "Systems";
        private static readonly string _componentsKey = "Components";
        private static readonly string _sessionKey = "Session";

        private static readonly string _methodsKey = "Methods";

        private static readonly string _rotationKey = "Rotation";
        // ReSharper restore ConvertToConstant.Local

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldProfile(object key)
        {
            if (key == null)
                return false;
            if (key is MyCubeBlock)
                return ProfileBlocksUpdate && ProfileGridsUpdate;
            if (key is MyCubeGrid)
                return ProfileGridsUpdate;
            if (key is IMyCharacter)
                return ProfileCharacterEntities;
            if (key is MyVoxelBase)
                return ProfileVoxels;
            if (key is MySessionComponentBase)
                return ProfileSessionComponentsUpdate;
            if (key is MyEntityComponentBase ecb)
                return ProfileEntityComponentsUpdate && ShouldProfile(ecb.Entity);
            return true;
        }

        internal static FatProfilerEntry EntityEntry(IMyEntity entity)
        {
            if (entity == null)
                return null;
            if (entity is MyCubeBlock block)
            {
                if (!ProfileBlocksUpdate || !ProfileGridsUpdate)
                    return null;
                var defEntry = EntityEntry(block.CubeGrid)?.GetFat(_blocksKey)?.GetFat(block.BlockDefinition);
                return ProfileBlocksIndividually ? defEntry?.GetFat(block) : defEntry;
            }
            if (entity is MyCubeGrid)
            {
                // ReSharper disable once ConvertIfStatementToReturnStatement
                if (!ProfileGridsUpdate)
                    return null;
                return FixedProfiler(ProfilerFixedEntry.Entities)?.GetFat(entity);
            }
            if (entity is IMyCharacter)
            {
                if (!ProfileCharacterEntities)
                    return null;
                return FixedProfiler(ProfilerFixedEntry.Entities)?.GetFat(entity);
            }
            if (entity is MyVoxelBase vox)
            {
                if (!ProfileVoxels)
                    return null;
                return vox.RootVoxel != null && vox.RootVoxel != vox
                    ? EntityEntry(vox.RootVoxel)?.GetFat(vox)
                    : FixedProfiler(ProfilerFixedEntry.Entities)?.GetFat(vox);
            }
            return null;
        }

        internal static FatProfilerEntry PlayerEntry(IMyIdentity id)
        {
            return FixedProfiler(ProfilerFixedEntry.Players)?.GetFat(id);
        }

        // Arguments ordered in this BS way for ease of IL use  (dup)
        internal static FatProfilerEntry GridSystemEntry(object system, IMyCubeGrid grid)
        {
            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (!ProfileGridSystemUpdates || !ProfileGridsUpdate || system == null)
                return null;
            Debug.Assert(!system.GetType().IsValueType, "Grid system was a value type.  Not good.");
            return EntityEntry(grid)?.GetFat(_systemsKey)?.GetFat(system);
        }

        internal static FatProfilerEntry EntityComponentEntry(MyEntityComponentBase component)
        {
            if (!ProfileEntityComponentsUpdate || component == null || component is MyCompositeGameLogicComponent)
                return null;
            return EntityEntry(component.Entity)?.GetFat(_componentsKey)?.GetFat(component);
        }

        internal static FatProfilerEntry SessionComponentEntry(MySessionComponentBase component)
        {
            if (!ProfileSessionComponentsUpdate || component == null)
                return null;
            return FixedProfiler(ProfilerFixedEntry.Session)?.GetFat(component);
        }

        #endregion

        #region Profiler Entry Factory

        private static SlimProfilerEntry MakeSlim(FatProfilerEntry caller, object key)
        {
            return new SlimProfilerEntry(caller);
        }

        private static FatProfilerEntry MakeFat(FatProfilerEntry caller, object key)
        {
            if (ProfileBlocksUpdateByOwner && key is MyCubeBlock block)
            {
                var identity = MySession.Static?.Players?.TryGetIdentity(block.OwnerId) ??
                               MySession.Static?.Players?.TryGetIdentity(block.BuiltBy);
                if (identity != null)
                {
                    var playerEntry = PlayerEntry(identity)?.GetFat(_blocksKey)?.GetFat(block.BlockDefinition);
                    if (playerEntry != null)
                    {
                        var result = new FatProfilerEntry(caller, playerEntry);
                        try
                        {
                            playerEntry.ChildUpdateTime.Add(key, result);
                        }
                        catch
                        {
                            // Ignore :/
                        }
                        return result;
                    }
                }
            }
            if (key is MyCharacter character)
            {
                var id = character.GetIdentity();
                var playerEntry = id != null ? PlayerEntry(id) : null;
                if (id != null && playerEntry != null)
                {
                    var result = new FatProfilerEntry(caller, playerEntry);
                    try
                    {
                        playerEntry.ChildUpdateTime.Add(key, result);
                    }
                    catch
                    {
                        // Ignore :/
                    }
                    return result;
                }
            }
            return new FatProfilerEntry(caller);
        }

        internal static SlimProfilerEntry MakeSlimExternal(FatProfilerEntry caller, object key)
        {
            var res = MakeSlim(caller, key);
            // add to end is read
            using (_profilingEntriesAllLock.ReadUsing())
                _profilingEntriesAll.Add(new KeyValuePair<GCHandle?, GCHandle>(GCHandle.Alloc(key, GCHandleType.Weak),
                    GCHandle.Alloc(res, GCHandleType.Weak)));
            return res;
        }

        internal static FatProfilerEntry MakeFatExternal(FatProfilerEntry caller, object key)
        {
            var res = MakeFat(caller, key);
            // add to end is read
            using (_profilingEntriesAllLock.ReadUsing())
                _profilingEntriesAll.Add(new KeyValuePair<GCHandle?, GCHandle>(GCHandle.Alloc(key, GCHandleType.Weak),
                    GCHandle.Alloc(res, GCHandleType.Weak)));
            return res;
        }

        #endregion

        #region Dump to Disk

        internal static void Dump(string path)
        {
            var tmp = new Dictionary<SlimProfilerEntry, ProfilerBlock>();
            var roots = new List<ProfilerBlock>();
            for (var i = 0; i < (int) ProfilerFixedEntry.Count; i++)
                roots.Add(DumpRecursive((ProfilerFixedEntry) i, FixedProfiler((ProfilerFixedEntry) i), tmp));
            using (var writer = File.CreateText(path))
                new XmlSerializer(typeof(List<ProfilerBlock>)).Serialize(writer, roots);
        }

        private static ProfilerBlock DumpRecursive(object owner, SlimProfilerEntry entry,
            IDictionary<SlimProfilerEntry, ProfilerBlock> result)
        {
            if (result.TryGetValue(entry, out ProfilerBlock block))
                return block;
            block = new ProfilerBlock()
            {
                TimeElapsed = entry.UpdateTime
            };
            block.SetOwner(owner);
            result.Add(entry, block);
            if (entry is FatProfilerEntry fat)
            {
                var keys = fat.ChildUpdateKeys();
                block.Children = new ProfilerBlock[keys.Count];
                var i = 0;
                foreach (var key in keys)
                    if (fat.ChildUpdateTime.TryGetValue(key, out SlimProfilerEntry child))
                        if (child.UpdateTime > 0)
                            block.Children[i++] = DumpRecursive(key, child, result);
                if (i != block.Children.Length)
                    Array.Resize(ref block.Children, i);
                Array.Sort(block.Children, new ProfilerBlockComparer());
            }
            return block;
        }

        private class ProfilerBlockComparer : IComparer<ProfilerBlock>
        {
            public int Compare(ProfilerBlock x, ProfilerBlock y)
            {
                if (x == null || y == null)
                    return 0;
                return -x.TimeElapsed.CompareTo(y.TimeElapsed);
            }
        }

        #endregion

        #region Return top entities by update time

        internal static IEnumerable<Tuple<string, double>> GetTopEntityUpdateTimes()
        {
            var tmp = new List<Tuple<string, double>>();
            FillEntityTimesRecursive(ProfilerFixedEntry.Entities, FixedProfiler(ProfilerFixedEntry.Entities), tmp);
            return tmp.OrderByDescending(e => e.Item2);
        }

        private static void FillEntityTimesRecursive(object owner, SlimProfilerEntry entry, IList<Tuple<string, double>> result)
        {
            if (entry is FatProfilerEntry fat)
            {
                var keys = fat.ChildUpdateKeys();
                if (keys.Count == 0)
                    result.Add(new Tuple<string, double>(ProfilerObjectIdentifier.Identify(owner), entry.UpdateTime));
                foreach (var key in keys)
                    if (fat.ChildUpdateTime.TryGetValue(key, out SlimProfilerEntry child))
                        if (child.UpdateTime > 0)
                            FillEntityTimesRecursive(key, child, result);
            }
            else
                result.Add(new Tuple<string, double>(ProfilerObjectIdentifier.Identify(owner), entry.UpdateTime));
        }

        #endregion
    }
}