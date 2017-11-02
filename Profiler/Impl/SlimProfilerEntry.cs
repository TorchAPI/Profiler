using System.Diagnostics;
using System.Threading;

namespace Profiler.Impl
{
    public class SlimProfilerEntry
    {
        internal readonly FatProfilerEntry[] _parents;
        private readonly Stopwatch _updateWatch = new Stopwatch();
        
        public double UpdateTime { get; private set; } = 0;

        private int _watchStarts;

        internal SlimProfilerEntry()
        {
            _parents = null;
        }

        internal SlimProfilerEntry(params FatProfilerEntry[] parents)
        {
            _parents = parents;
        }

        internal void Start()
        {
            if (Interlocked.Add(ref _watchStarts, 1) == 1)
            {
                if (_parents!=null)
                    foreach (var p in _parents)
                        p?.Start();
                _updateWatch.Start();
            }
        }

        internal void Stop()
        {
            if (Interlocked.Add(ref _watchStarts, -1) == 0)
            {
                _updateWatch.Stop();
                if (_parents != null)
                    foreach (var p in _parents)
                        p?.Stop();
            }
        }

        internal void Rotate()
        {
            UpdateTime = _updateWatch.Elapsed.TotalSeconds / ProfilerData.RotateInterval;
            _updateWatch.Reset();
            Debug.Assert(_watchStarts == 0, "A watch wasn't stopped");
            _watchStarts = 0;
        }
    }
}