using Profiler.Core;
using VRageMath.Spatial;

namespace Profiler.Basics
{
    public class ClusterTreeProfiler : BaseProfiler<MyClusterTree.MyCluster>
    {
        public static bool Active;

        public ClusterTreeProfiler()
        {
            Active = true;
        }
        protected override bool TryAccept(in ProfilerResult profilerResult, out MyClusterTree.MyCluster key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            if (profilerResult.GameEntity is not MyClusterTree.MyCluster cluster)
                return false;
            key = cluster;
            return true;
        }

        public override void Dispose()
        {
            base.Dispose();
            Active = false;
        }
    }
}