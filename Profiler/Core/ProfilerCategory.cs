namespace Profiler.Core
{
    public enum ProfilerCategory
    {
        General, //MyEntity or MyGameLogic
        Scripts,
        Update,
        UpdateNetwork,
        UpdateNetworkEvent,
        UpdateReplication,
        UpdateParallelWait,
        UpdateParallelRun,
        UpdateSessionComponents,
        UpdateSessionComponentsAll,
        UpdateGps,
        Lock,
        Frame,
        Physics,
        Custom,
    }
}