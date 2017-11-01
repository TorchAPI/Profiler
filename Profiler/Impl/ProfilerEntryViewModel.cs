using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

#pragma warning disable 649
        [ReflectedGetter(Name = "Keys")]
        private static readonly Func<ConditionalWeakTable<object, SlimProfilerEntry>, ICollection<object>> _weakTableKeys;
#pragma warning restore 649

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

        public MtObservableList<ProfilerEntryViewModel> Children { get; } = new MtObservableList<ProfilerEntryViewModel>();
        public MtObservableList<ProfilerEntryViewModel> ChildrenSorted { get; } = new MtObservableList<ProfilerEntryViewModel>();

        private ProfilerFixedEntry _fixedEntry = ProfilerFixedEntry.Count;
        private readonly WeakReference<object> _owner = new WeakReference<object>(null);
        private WeakReference<object>[] _getterExtra;
        private Func<SlimProfilerEntry> _getter;

        public bool IsExpanded { get; set; }
        public bool IsSelected { get; set; }

        #region Getter Impls
        private SlimProfilerEntry GetterImplEntity()
        {
            return _owner.TryGetTarget(out var own) ? ProfilerData.EntityEntry((IMyEntity)own) : null;
        }

        private SlimProfilerEntry GetterImplEntityComponent()
        {
            return _owner.TryGetTarget(out var own)
                ? ProfilerData.EntityComponentEntry((MyEntityComponentBase)own)
                : null;
        }

        private SlimProfilerEntry GetterImplGridSystem()
        {
            return _owner.TryGetTarget(out var own) && _getterExtra[0].TryGetTarget(out object grd) ? ProfilerData.GridSystemEntry(own, (IMyCubeGrid)grd) : null;
        }
        #endregion

        #region SetTarget

        internal void SetTarget(ProfilerFixedEntry owner)
        {
            if (owner == ProfilerFixedEntry.Count)
                throw new ArgumentOutOfRangeException(nameof(owner), "Must not be the count enum");
            _fixedEntry = owner;
            _owner.SetTarget(null);
            _getterExtra = null;
            // we can capture here since its a value type
            _getter = () => ProfilerData.FixedProfiler(owner);
        }

        internal void SetTarget(IMyEntity owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            _owner.SetTarget(owner);
            _getterExtra = new WeakReference<object>[0];
            _getter = GetterImplEntity;
        }

        internal void SetTarget(MyEntityComponentBase owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            _owner.SetTarget(owner);
            _getterExtra = new WeakReference<object>[0];
            _getter = GetterImplEntityComponent;
        }

        internal void SetTarget(IMyCubeGrid grid, object owner)
        {
            _fixedEntry = ProfilerFixedEntry.Count;
            _owner.SetTarget(owner);
            _getterExtra = new[] { new WeakReference<object>(grid) };
            _getter = GetterImplGridSystem;
        }
        #endregion

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
                bool lostHandle = !_owner.TryGetTarget(out owner);
                if (_getterExtra != null && !lostHandle)
                    foreach (WeakReference<object> ext in _getterExtra)
                        if (!ext.TryGetTarget(out _))
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
                    ICollection<object> keys = _weakTableKeys(fat.ChildUpdateTime);
                    while (Children.Count > keys.Count)
                        Children.RemoveAt(Children.Count - 1);
                    var id = 0;
                    foreach (object key in keys)
                    {
                        if (fat.ChildUpdateTime.TryGetValue(key, out SlimProfilerEntry child))
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
                    using (ChildrenSorted.DeferredUpdate())
                    {
                        var sortedEnumerable = Children.OrderBy(x => (int)(-x.UpdateTime * 1e6));
                        if (Children.Count > PaginationCount)
                        {
                            var pageCount = (int)Math.Ceiling(Children.Count / (float)PaginationCount);
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
                                if (k.UpdateTime > 0)
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

        private readonly MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler> _propertyChangedEvent =
            new MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler>();

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
