using System.Threading;
using System.Threading.Tasks;
using Profiler.Utils.Patches;

namespace Profiler.Utils
{
    public static class GameLoopObserver
    {
        public static Task MoveToGameLoop(CancellationToken canceller = default)
        {
            return MySession_Update.MoveToGameLoop(canceller);
        }
    }
}