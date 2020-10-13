using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Profiler.Core;
using Sandbox.Game.Entities.Blocks;

namespace Profiler.Interactive
{
    public sealed class ProgrammableBlockProfiler : IProfilerObserver, IDisposable
    {
        readonly GameEntityMask _mask;
        readonly ConcurrentDictionary<MyProgrammableBlock, ProfilerEntry> _profilerEntries;
        readonly Func<MyProgrammableBlock, ProfilerEntry> _makeProfilerEntity;

        public ProgrammableBlockProfiler(GameEntityMask mask)
        {
            _mask = mask;
            _profilerEntries = new ConcurrentDictionary<MyProgrammableBlock, ProfilerEntry>();
            _makeProfilerEntity = _ => ProfilerEntry.Pool.Instance.UnpoolOrCreate();
        }

        public IEnumerable<(MyProgrammableBlock PB, ProfilerEntry ProfilerEntry)> GetProfilerEntries()
        {
            return _profilerEntries.Select(p => (p.Key, p.Value)).ToArray();
        }

        public void OnProfileComplete(in ProfilerResult profilerResult)
        {
            if (profilerResult.ProfileType != ProfileType.ProgrammableBlock) return;

            var programmableBlock = (MyProgrammableBlock) profilerResult.GameEntity;
            if (programmableBlock.Closed) return;
            if (!_mask.AcceptBlock(programmableBlock)) return;

            var profilerEntry = _profilerEntries.GetOrAdd(programmableBlock, _makeProfilerEntity);
            profilerEntry.Add(profilerResult);
        }

        public void Dispose()
        {
            ProfilerEntry.Pool.Instance.PoolAll(_profilerEntries.Values);
        }
    }
}