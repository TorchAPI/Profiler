using System;

namespace Profiler.Util
{
    /// <summary>
    /// IDisposable implementation that invokes given Action when disposed.
    /// </summary>
    public sealed class ActionDisposable : IDisposable
    {
        readonly Action _onDispose;

        /// <summary>
        /// Instantiate with an Action to be invoked when this instance is disposed.
        /// </summary>
        /// <param name="onDispose">Action to invoke on disposal.</param>
        public ActionDisposable(Action onDispose)
        {
            _onDispose = onDispose;
        }

        public void Dispose()
        {
            _onDispose?.Invoke();
        }
    }
}