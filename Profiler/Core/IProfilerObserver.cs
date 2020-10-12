namespace Profiler.Core
{
    /// <summary>
    /// Implement this interface and put the instance in `ProfilerPatch.AddObserver()`
    /// to receive ProfilerResult's for each update method in the game loop.
    /// </summary>
    public interface IProfilerObserver
    {
        void OnProfileComplete(in ProfilerResult profilerResult);
    }
}