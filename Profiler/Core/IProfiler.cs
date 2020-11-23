namespace Profiler.Core
{
    /// <summary>
    /// Receive and consume profiling data.
    /// </summary>
    /// <remarks>Implementation can be consumed via `ProfilerResultQueue.Profile()`.</remarks>
    public interface IProfiler
    {
        /// <summary>
        /// Called when a profiled method finished running.
        /// </summary>
        /// <remarks>
        /// Can be called multiple times (a lot of times) every frame, depending on the number of patched methods and patched objects in the game.
        /// </remarks>
        /// <remarks>
        /// Called in a single worker thread.
        /// </remarks>
        /// <param name="profilerResult">Profiling data of the method that just finished running.</param>
        void ReceiveProfilerResult(in ProfilerResult profilerResult);
    }
}