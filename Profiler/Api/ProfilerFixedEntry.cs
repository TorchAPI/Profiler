namespace Profiler.Api
{
    /// <summary>
    /// Indicates a "fixed" profiler entry.  These always exist and will not be moved.
    /// </summary>
    public enum ProfilerFixedEntry
    {
        Entities,
        Session,
        Players,
        Count
    }
}