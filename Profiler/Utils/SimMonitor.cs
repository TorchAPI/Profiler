using System.Linq;
using System.Threading.Tasks;
using Sandbox.Game.Multiplayer;

namespace Profiler.Utils
{
    public sealed class SimMonitor
    {
        readonly float[] _simSpeeds;
        readonly int _seconds;

        public SimMonitor(int seconds)
        {
            _seconds = seconds;
            _simSpeeds = new float[seconds];
        }

        public float Max => _simSpeeds.Max();
        public float Min => _simSpeeds.Min();
        public float Avg => _simSpeeds.Average();

        public async Task Monitor()
        {
            for (var i = 0; i < _seconds; i++)
            {
                var simSpeed = Sync.ServerSimulationRatio;
                _simSpeeds[i] = simSpeed;
                await Task.Delay(1000);
            }
        }
    }
}