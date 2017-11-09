using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Profiler.Api;
using Torch.Collections;
using Torch.Utils;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Profiler.Impl
{
    public class ProfilerEntryViewModel : IProfilerEntryViewModel
    {
        /// <summary>
        /// Applies to <see cref="ChildrenSorted"/>
        /// </summary>
        private const int PaginationCount = 50;

        /// <summary>
        /// Times below this won't be shown.
        /// </summary>
        private const double DisplayTimeThreshold = 1e-6;

        internal ProfilerEntryViewModel()
        {
            Children.PropertyChanged += ObservedChanged;
            ChildrenSorted.PropertyChanged += ObservedChanged;
        }

        private void ObservedChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            if (!propertyChangedEventArgs.PropertyName.Equals(nameof(Children.IsObserved)))
                return;
            if ((Children.IsObserved || ChildrenSorted.IsObserved) && _childrenUpdateDeferred)
                Update();
        }

        public string OwnerName { get; private set; } = "";
        public double UpdateTime { get; private set; }
        public double UpdateTimeMs => 1000 * UpdateTime;
        public double UpdateLoadPercent => 100 * UpdateTime / MyEngineConstants.UPDATE_STEP_SIZE_IN_SECONDS;

        public string UpdateDescription => ProfilerData.DisplayLoadPercentage
            ? $"{UpdateLoadPercent:F2} %"
            : $"{UpdateTimeMs:F3} ms";

        public MtObservableList<ProfilerEntryViewModel> Children { get; } =
            new MtObservableList<ProfilerEntryViewModel>();

        public MtObservableList<ProfilerEntryViewModel> ChildrenSorted { get; } =
            new MtObservableList<ProfilerEntryViewModel>();

        private ProfilerFixedEntry _fixedEntry = ProfilerFixedEntry.Count;
        private GCHandle _owner;
        private GCHandle[] _getterExtra;
        private Func<SlimProfilerEntry> _getter;

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        #region Getter Impls

        private SlimProfilerEntry GetterImplEntity()
        {
            return !_owner.IsAllocated ? null : ProfilerData.EntityEntry(_owner.Target as IMyEntity);
        }

        private SlimProfilerEntry GetterImplEntityComponent()
        {
            return !_owner.IsAllocated
                ? null
                : ProfilerData.EntityComponentEntry(_owner.Target as MyEntityComponentBase);
        }

        private SlimProfilerEntry GetterImplGridSystem()
        {
            return !_owner.IsAllocated || !_getterExtra[0].IsAllocated
                ? null
                : ProfilerData.GridSystemEntry(_owner.Target, _getterExtra[0].Target as IMyCubeGrid);
        }

        #endregion

        #region SetTarget

        private void FreeHandles()
        {
            if (_owner.IsAllocated)
                _owner.Free();
            if (_getterExtra != null)
            {
                foreach (var k in _getterExtra)
                    k.Free();
                _getterExtra = null;
            }
        }

        internal void SetTarget(ProfilerFixedEntry owner)
        {
            if (owner == ProfilerFixedEntry.Count)
                throw new ArgumentOutOfRangeException(nameof(owner), "Must not be the count enum");
            _fixedEntry = owner;
            FreeHandles();
            _owner = GCHandle.Alloc(owner, GCHandleType.Weak);
            // we can capture here since its a value type
            _getter = () => ProfilerData.FixedProfiler(owner);
        }

        internal void SetTarget(IMyEntity owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            FreeHandles();
            _owner = GCHandle.Alloc(owner, GCHandleType.Weak);
            _getter = GetterImplEntity;
        }

        internal void SetTarget(MyEntityComponentBase owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            FreeHandles();
            _owner = GCHandle.Alloc(owner, GCHandleType.Weak);
            _getter = GetterImplEntityComponent;
        }

        internal void SetTarget(IMyCubeGrid grid, object owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            FreeHandles();
            _owner = GCHandle.Alloc(owner, GCHandleType.Weak);
            _getterExtra = new[] {GCHandle.Alloc(grid, GCHandleType.Weak)};
            _getter = GetterImplGridSystem;
        }
        #endregion

        ~ProfilerEntryViewModel()
        {
            FreeHandles();
        }

        /// <summary>
        /// Called to update the values of this view model without changing the target.
        /// </summary>
        /// <param name="forcePropertyRefresh">Forces all properties to refresh</param>
        /// <returns>False if the target was lost</returns>
        internal bool Update(bool forcePropertyRefresh = false)
        {
            object owner;
            if (_fixedEntry == ProfilerFixedEntry.Count)
            {
                bool lostHandle = !_owner.IsAllocated || _owner.Target == null;
                if (_getterExtra != null && !lostHandle)
                    foreach (var ext in _getterExtra)
                        if (!ext.IsAllocated || ext.Target == null)
                        {
                            lostHandle = true;
                            break;
                        }
                if (lostHandle)
                {
                    OwnerName = "Lost Handle";
                    OnPropertyChanged(nameof(OwnerName));
                    UpdateTime = 0;
                    OnPropertyChanged(nameof(UpdateTime));
                    Children.Clear();
                    return false;
                }
                owner = _owner.Target;
            }
            else
                owner = _fixedEntry;
            UpdateInternal(owner, _getter(), forcePropertyRefresh);
            return true;
        }

        private const string _noData = "No Data";

        private bool _childrenUpdateDeferred = false;
        private bool _wasPaged = false;

        private void UpdateInternal(object owner, SlimProfilerEntry entry, bool forcePropertyUpdate = false)
        {
            if (entry == null)
            {
                if (!OwnerName.Equals(_noData) || forcePropertyUpdate)
                {
                    OwnerName = _noData;
                    OnPropertyChanged(nameof(OwnerName));
                }
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (UpdateTime != 0 || forcePropertyUpdate)
                {
                    UpdateTime = 0;
                    OnPropertyChanged(nameof(UpdateTime));
                }
                if (Children.Count > 0)
                    Children.Clear();
                return;
            }
            string ownerId = ProfilerObjectIdentifier.Identify(owner);
            if (!ownerId.Equals(OwnerName) || forcePropertyUpdate)
            {
                OwnerName = ownerId;
                OnPropertyChanged(nameof(OwnerName));
            }
            UpdateTime = entry.UpdateTime;
            OnPropertyChanged(nameof(UpdateTime));

            if (entry is FatProfilerEntry fat)
            {
                if (ChildrenSorted.IsObserved || Children.IsObserved || forcePropertyUpdate)
                {
                    _childrenUpdateDeferred = false;
                    ICollection<object> keys = fat.ChildUpdateKeys();
                    var id = 0;
                    foreach (object key in keys)
                    {
                        if (fat.ChildUpdateTime.TryGetValue(key, out SlimProfilerEntry child) &&
                            child.UpdateTime > DisplayTimeThreshold)
                        {
                            if (id >= Children.Count)
                            {
                                var result = new ProfilerEntryViewModel();
                                result.UpdateInternal(key, child, forcePropertyUpdate);
                                Children.Add(result);
                                id++;
                            }
                            else
                            {
                                Children[id++].UpdateInternal(key, child, forcePropertyUpdate);
                            }
                        }
                    }
                    while (Children.Count > id)
                        Children.RemoveAt(Children.Count - 1);
                    using (ChildrenSorted.DeferredUpdate())
                    {
                        var sortedEnumerable = Children.OrderBy(x => (int) (-x.UpdateTime / DisplayTimeThreshold));
                        if (Children.Count > PaginationCount)
                        {
                            var pageCount = (int) Math.Ceiling(Children.Count / (float) PaginationCount);
                            if (_wasPaged)
                                while (ChildrenSorted.Count > pageCount)
                                    ChildrenSorted.RemoveAt(ChildrenSorted.Count - 1);
                            else
                                ChildrenSorted.Clear();
                            while (ChildrenSorted.Count < pageCount)
                                ChildrenSorted.Add(new ProfilerEntryViewModel());

                            using (var iterator = sortedEnumerable.GetEnumerator())
                            {
                                for (var i = 0; i < pageCount; i++)
                                {
                                    ChildrenSorted[i].OwnerName =
                                        $"Items {i * PaginationCount + 1} to {i * PaginationCount + PaginationCount}";
                                    ChildrenSorted[i].OnPropertyChanged(nameof(OwnerName));
                                    FillPage(ChildrenSorted[i], iterator);
                                }
                            }
                            _wasPaged = true;
                        }
                        else
                        {
                            ChildrenSorted.Clear();
                            foreach (var k in sortedEnumerable)
                                ChildrenSorted.Add(k);
                            _wasPaged = false;
                        }
                    }
                }
                else
                    _childrenUpdateDeferred = true;
            }
            else
            {
                Children.Clear();
                ChildrenSorted.Clear();
            }
        }

        private void FillPage(ProfilerEntryViewModel target, IEnumerator<ProfilerEntryViewModel> items)
        {
            using (target.Children.DeferredUpdate())
            using (target.ChildrenSorted.DeferredUpdate())
            {
                var count = 0;
                var time = 0.0;
                while (count < PaginationCount)
                {
                    if (!items.MoveNext() || items.Current == null)
                        break;
                    ProfilerEntryViewModel entry = items.Current;
                    if (count < target.Children.Count)
                        target.Children[count] = entry;
                    else
                        target.Children.Add(entry);
                    if (count < target.ChildrenSorted.Count)
                        target.ChildrenSorted[count] = entry;
                    else
                        target.ChildrenSorted.Add(entry);
                    time += entry.UpdateTime;
                    count++;
                }
                while (target.Children.Count > count)
                    target.Children.RemoveAt(target.Children.Count - 1);
                while (target.ChildrenSorted.Count > count)
                    target.ChildrenSorted.RemoveAt(target.ChildrenSorted.Count - 1);
                target.UpdateTime = time;
                target.OnPropertyChanged(nameof(UpdateTime));
            }
        }

        private readonly MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler> _propertyChangedEvent
            = new MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler>();

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged
        {
            add => _propertyChangedEvent.Add(value);
            remove => _propertyChangedEvent.Remove(value);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            if (propertyName == nameof(UpdateTime))
            {
                OnPropertyChanged(nameof(UpdateLoadPercent));
                OnPropertyChanged(nameof(UpdateTimeMs));
                OnPropertyChanged(nameof(UpdateDescription));
            }
            _propertyChangedEvent.Raise(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}