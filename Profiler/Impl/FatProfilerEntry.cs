using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Torch.Utils;

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
            _childUpdateTimeCreateValueFat = (key) => ProfilerData.MakeFatExternal(this, key);
            _childUpdateTimeCreateValueSlim = (key) => ProfilerData.MakeSlimExternal(this, key);
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

#pragma warning disable 649
        [ReflectedGetter(Name = "Keys")]
        private static readonly Func<ConditionalWeakTable<object, SlimProfilerEntry>, ICollection<object>> _weakTableKeys;
#pragma warning restore 649

        /// <summary>
        /// Note: This method performs an allocation of a new list.  Should not be in the hot path.
        /// </summary>
        /// <returns>collection of child keys</returns>
        internal ICollection<object> ChildUpdateKeys()
        {
            return _weakTableKeys(ChildUpdateTime);
        }
    }
}