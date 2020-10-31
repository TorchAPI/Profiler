namespace Profiler.Core
{
    /// <summary>
    /// Receive and consume profiling data.
    /// </summary>
    /// <remarks>Should be implemented and fed to ProfilerPatch.AddProfiler() to receive profiling data of the game.</remarks>
    public interface IProfiler
    {
        /// <summary>
        /// Called when a profiled method finished running.
        /// </summary>
        /// <remarks>Can be called multiple times per frame according to
        /// the number of patched methods in ProfilerPatch and
        /// the number of entities in the game.</remarks>
        /// <param name="profilerResult">Profiling data of the method that just finished running.</param>
        void OnProfileComplete(in ProfilerResult profilerResult);
    }
}