using System.Threading;

namespace Profiler.Database
{
    public interface IDbProfiler
    {
        void StartProfiling(CancellationToken canceller);
    }
}