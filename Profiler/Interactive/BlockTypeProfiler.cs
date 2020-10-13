using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Game.Entities;

namespace Profiler.Interactive
{
    public sealed class BlockTypeProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<Type, ProfilerEntry> _profilerEntries;
        readonly Func<Type, ProfilerEntry> _makeProfilerEntity;

        public BlockTypeProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<Type, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(Type Type, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            var block = profilerResult.GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return;
            if (!_mask.AcceptBlock(block)) return;
            if (block.BlockDefinition == null) return;

            var profilerEntry = _profilerEntries.GetOrAdd(block.GetType(), _makeProfilerEntity);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}