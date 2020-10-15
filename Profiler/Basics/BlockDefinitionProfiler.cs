using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Profiler.Util;
using Sandbox.Definitions;
using Sandbox.Game.Entities;

namespace Profiler.Basics
{
    public sealed class BlockDefinitionProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<MyCubeBlockDefinition, ProfilerEntry> _profilerEntries;
        readonly Func<MyCubeBlockDefinition, ProfilerEntry> _makeProfilerEntity;

        public BlockDefinitionProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<MyCubeBlockDefinition, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(MyCubeBlockDefinition BlockDefinition, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(kv => (kv.Key, kv.Value)).ToArray();
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.Entrypoint != Entrypoint.General) return;
            
            var block = profilerResult.GameEntity.GetParentEntityOfType<MyCubeBlock>();
            if (block == null) return;
            if (!_mask.AcceptBlock(block)) return;
            if (block.BlockDefinition == null) return;

            var profilerEntry = _profilerEntries.GetOrAdd(block.BlockDefinition, _makeProfilerEntity);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}