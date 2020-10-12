using System;

namespace Profiler.Util
{
    public sealed class Disposable : IDisposable
    {
        readonly Action _onDispose;

        public Disposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose();
        }
    }
}