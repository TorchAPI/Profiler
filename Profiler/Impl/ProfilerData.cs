using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
        internal static bool ProfileBlocksUpdate = true;
        internal static bool ProfileBlocksIndividually = true;
        internal static bool ProfileEntityComponentsUpdate = true;
        internal static bool ProfileGridSystemUpdates = true;
        internal static bool ProfileSessionComponentsUpdate = true;
        internal static bool ProfileSingleMethods = true;
        internal static bool ProfilerVoxels = true;
        internal static bool ProfileCharacterEntities = true;
        internal static bool DisplayLoadPercentage = false;
        internal static bool DisplayModNames = true;

        internal static bool AnonymousProfilingDumps = false;

        // Doesn't update properly during runtime so not exposed.
        private static bool ProfileBlocksUpdateByOwner = true;

        #region Msil Method Handles

        private const BindingFlags INSTANCE_FLAGS = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        private const BindingFlags STATIC_FLAGS = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        private static MethodInfo Method(Type type, string name, BindingFlags flags)
        {
            return type.GetMethod(name, flags) ?? throw new Exception($"Couldn't find method {name} on {type}");
        }

        internal static readonly MethodInfo ProfilerEntryStart = Method(typeof(SlimProfilerEntry), nameof(SlimProfilerEntry.Start), INSTANCE_FLAGS);
        internal static readonly MethodInfo ProfilerEntryStop = Method(typeof(SlimProfilerEntry), nameof(SlimProfilerEntry.Stop), INSTANCE_FLAGS);
        internal static readonly MethodInfo GetEntityProfiler = Method(typeof(ProfilerData), nameof(EntityEntry), STATIC_FLAGS);
        internal static readonly MethodInfo GetGridSystemProfiler = Method(typeof(ProfilerData), nameof(GridSystemEntry), STATIC_FLAGS);
        internal static readonly MethodInfo GetEntityComponentProfiler = Method(typeof(ProfilerData), nameof(EntityComponentEntry), STATIC_FLAGS);
        internal static readonly MethodInfo GetSessionComponentProfiler = Method(typeof(ProfilerData), nameof(SessionComponentEntry), STATIC_FLAGS);
        internal static readonly MethodInfo GetSlim = Method(typeof(FatProfilerEntry), nameof(FatProfilerEntry.GetSlim), INSTANCE_FLAGS);
        internal static readonly MethodInfo GetFat = Method(typeof(FatProfilerEntry), nameof(FatProfilerEntry.GetFat), INSTANCE_FLAGS);
        internal static readonly FieldInfo FieldProfileSingleMethods = typeof(ProfilerData).GetField(nameof(ProfileSingleMethods), STATIC_FLAGS) ?? throw new Exception($"Couldn't find profile single methods setting");
        internal static readonly MethodInfo DoRotateEntries = Method(typeof(ProfilerData), nameof(RotateEntries), STATIC_FLAGS);
        #endregion

        #region View Models
        internal static ProfilerEntryViewModel BindView(ProfilerEntryViewModel cache = null)
        {
            if (cache != null)
                return cache;
            using (_boundViewModelsLock.WriteUsing())
                _boundViewModels.Add(new WeakReference<ProfilerEntryViewModel>(cache = new ProfilerEntryViewModel()));
            return cache;
        }

        private static readonly ReaderWriterLockSlim _boundViewModelsLock = new ReaderWriterLockSlim();
        private static readonly List<WeakReference<ProfilerEntryViewModel>> _boundViewModels = new List<WeakReference<ProfilerEntryViewModel>>();
        #endregion

        #region Rotation
        public const int RotateInterval = 300;
        private static int _rotateIntervalCounter = RotateInterval - 1;
        private static void RotateEntries()
        {
            bool refreshedModels = false;
            if (Interlocked.Exchange(ref _forceFullPropertyUpdate, 0) != 0)
            {
                RefreshModels();
                refreshedModels = true;
            }
            if (_rotateIntervalCounter++ > RotateInterval)
            {
                _rotateIntervalCounter = 0;
                // we remove == write
                using (_profilingEntriesAllLock.WriteUsing())
                {
                    var i = 0;
                    while (i < _profilingEntriesAll.Count)
                    {
                        if (_profilingEntriesAll[i].TryGetTarget(out SlimProfilerEntry result))
                        {
                            result.Rotate();
                            i++;
                        }
                        else
                        {
                            _profilingEntriesAll.RemoveAtFast(i);
                        }
                    }
                }
                if (!refreshedModels)
                    RefreshModels();
            }
        }

        private class AsyncViewModelUpdate : IWork
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
                Interlocked.Increment(ref _workers);
                while (true)
                {
                    WeakReference<ProfilerEntryViewModel> target;
                    using (_boundViewModelsLock.ReadUsing())
                    {
                        var offset = _offset++;
                        if (offset >= _boundViewModels.Count)
                            break;
                        target = _boundViewModels[offset];
                    }
                    if (target != null && (!target.TryGetTarget(out ProfilerEntryViewModel model) || !model.Update()))
                        target.SetTarget(null);
                }
                // Last worker leaving
                if (Interlocked.Decrement(ref _workers) == 0)
                {
                    using (_boundViewModelsLock.UpgradableReadUsing())
                    {
                        var i = 0;
                        while (i < _boundViewModels.Count)
                        {
                            if (!_boundViewModels[i].TryGetTarget(out _))
                                using (_boundViewModelsLock.WriteUsing())
                                    _boundViewModels.RemoveAtFast(i);
                            else
                                i++;
                        }
                    }
                }
            }
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
        internal static readonly List<WeakReference<SlimProfilerEntry>> _profilingEntriesAll = new List<WeakReference<SlimProfilerEntry>>();
        internal static readonly FatProfilerEntry[] Fixed;

        internal static FatProfilerEntry FixedProfiler(ProfilerFixedEntry item)
        {
            return Fixed[(int)item] ?? throw new InvalidOperationException($"Fixed profiler {item} doesn't exist");
        }

        static ProfilerData()
        {
            Fixed = new FatProfilerEntry[(int)ProfilerFixedEntry.Count];
            // add to end is "read"
            using (_profilingEntriesAllLock.ReadUsing())
                for (var i = 0; i < Fixed.Length; i++)
                {
                    Fixed[i] = new FatProfilerEntry();
                    _profilingEntriesAll.Add(new WeakReference<SlimProfilerEntry>(Fixed[i]));
                }
        }

        // ReSharper disable ConvertToConstant.Local
        // Don't make these constants.  We need to keep the reference alive and the same for the weak table.
        private static readonly string _blocksKey = "Blocks";
        private static readonly string _systemsKey = "Systems";
        private static readonly string _componentsKey = "Components";
        private static readonly string _sessionKey = "Session";
        private static readonly string _methodsKey = "Methods";
        // ReSharper restore ConvertToConstant.Local

        internal static FatProfilerEntry EntityEntry(IMyEntity entity)
        {
            if (entity == null)
                return null;
            if (entity is MyCubeBlock block)
            {
                if (!ProfileBlocksUpdate || !ProfileGridsUpdate)
                    return null;
                var defEntry =  EntityEntry(block.CubeGrid)?.GetFat(_blocksKey)?.GetFat(block.BlockDefinition);
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
                if (!ProfilerVoxels)
                    return null;
                return FixedProfiler(ProfilerFixedEntry.Entities)?.GetFat(entity);
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
                _profilingEntriesAll.Add(new WeakReference<SlimProfilerEntry>(res));
            return res;
        }

        internal static FatProfilerEntry MakeFatExternal(FatProfilerEntry caller, object key)
        {
            var res = MakeFat(caller, key);
            // add to end is read
            using (_profilingEntriesAllLock.ReadUsing())
                _profilingEntriesAll.Add(new WeakReference<SlimProfilerEntry>(res));
            return res;
        }

        #endregion

        #region Dump to Disk
        internal static void Dump(string path)
        {
            var tmp = new Dictionary<SlimProfilerEntry, ProfilerBlock>();
            var roots = new List<ProfilerBlock>();
            for (var i = 0; i < (int)ProfilerFixedEntry.Count; i++)
                roots.Add(DumpRecursive((ProfilerFixedEntry)i, FixedProfiler((ProfilerFixedEntry)i), tmp));
            using (var writer = File.CreateText(path))
                new XmlSerializer(typeof(List<ProfilerBlock>)).Serialize(writer, roots);
        }

        private static ProfilerBlock DumpRecursive(object owner, SlimProfilerEntry entry, IDictionary<SlimProfilerEntry, ProfilerBlock> result)
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
    }
}
