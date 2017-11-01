using System;
using System.Runtime.CompilerServices;

namespace Profiler.Impl
{
    public class FatProfilerEntry : SlimProfilerEntry
    {
        private readonly ConditionalWeakTable<object, SlimProfilerEntry>.CreateValueCallback
            _childUpdateTimeCreateValueFat;
        private readonly ConditionalWeakTable<object, SlimProfilerEntry>.CreateValueCallback
            _childUpdateTimeCreateValueSlim;
        internal readonly ConditionalWeakTable<object, SlimProfilerEntry> ChildUpdateTime = new ConditionalWeakTable<object, SlimProfilerEntry>();

        internal FatProfilerEntry() : this(null)
        {
        }

        internal FatProfilerEntry(params FatProfilerEntry[] parents) : base(parents)
        {
            _childUpdateTimeCreateValueFat = (key) =>
            {
                var result = ProfilerData.MakeFat(this, key);
                lock (ProfilerData.ProfilingEntriesAll)
                    ProfilerData.ProfilingEntriesAll.Add(new WeakReference<SlimProfilerEntry>(result));
                return result;
            };
            _childUpdateTimeCreateValueSlim = (key) =>
            {
                var result = ProfilerData.MakeSlim(this, key);
                lock (ProfilerData.ProfilingEntriesAll)
                    ProfilerData.ProfilingEntriesAll.Add(new WeakReference<SlimProfilerEntry>(result));
                return result;
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SlimProfilerEntry GetSlim(object key)
        {
            return ChildUpdateTime.GetValue(key, _childUpdateTimeCreateValueSlim);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal FatProfilerEntry GetFat(object key)
        {
            return (FatProfilerEntry)ChildUpdateTime.GetValue(key, _childUpdateTimeCreateValueFat);
        }
    }
}