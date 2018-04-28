using System;
using System.Collections.Generic;
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

        [ThreadStatic]
        private static Stack<SlimProfilerEntry> _activeStor;

        public static SlimProfilerEntry GetActive =>
            _activeStor != null && _activeStor.Count > 0 ? _activeStor.Peek() : null;

        internal void Start()
        {
            if (Interlocked.Add(ref _watchStarts, 1) == 1)
            {
                if (_parents!=null)
                    foreach (var p in _parents)
                        p?.Start();

                var active = _activeStor;
                if (active == null)
                    _activeStor = active = new Stack<SlimProfilerEntry>();
                active.Push(this);

                _updateWatch.Start();
            }
        }

        internal void Stop()
        {
            if (Interlocked.Add(ref _watchStarts, -1) == 0)
            {
                _updateWatch.Stop();

                var active = _activeStor;
                if (active != null && active.Count > 0)
                {
                    var p = active.Pop();
                    if (p != this)
                    {
                        active.Push(p);
                        Console.WriteLine("Bad active head");
                    }
                }
                else
                {
                    Console.WriteLine("No active head");
                }

                if (_parents != null)
                    foreach (var p in _parents)
                        p?.Stop();
            }
        }

        private uint _lastTickId;
        internal void Rotate(uint tickId)
        {
            // Modulo math.  If tickId rolls around, this still works.
            uint ticksPassed = unchecked(tickId - _lastTickId);
            if (ticksPassed <= 100)
                return;
            _lastTickId = tickId;
            UpdateTime = _updateWatch.Elapsed.TotalSeconds / ticksPassed;
            _updateWatch.Reset();
            _watchStarts = 0;
        }
    }
}