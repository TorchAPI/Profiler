using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InfluxDB.Client.Writes;
using NLog;
using Profiler.Core;
using Torch.Server.InfluxDb;

namespace Profiler
{
    public sealed class ProfilerDbClient
    {
        const int SamplingTicks = 900;
        const int MaxGridCount = 10;

        static readonly Logger _logger = LogManager.GetCurrentClassLogger();
        readonly InfluxDbClient _dbClient;
        bool _running;

        public ProfilerDbClient(InfluxDbClient dbClient)
        {
            _dbClient = dbClient;
        }

        public async Task StartProfiling()
        {
            if (_running)
            {
                _logger.Warn("already running");
                return;
            }

            _running = true;

            _logger.Info("Starting profiler...");

            while (_running)
            {
                await ProfilerData.WaitUntilNextFrame();

                var request = new ProfilerRequest(ProfilerRequestType.Grid, SamplingTicks);
                var doneProfiling = false;

                request.OnFinished += results =>
                {
                    // will be executed when the profiling is done
                    doneProfiling = true;
                    OnProfilingFinished(results);
                };

                if (!ProfilerData.Submit(request, null, null, null))
                {
                    _logger.Warn("Profiler is already active. Only one profiling command can be active at a time");

                    // wait until whatever ongoing profiling is done (for a long enough time)
                    await Task.Delay(TimeSpan.FromSeconds(20f));
                    continue;
                }

                _logger.Trace("Profiler round started");

                // wait until the profiling is done
                while (!doneProfiling && _running)
                {
                    // set enough margin to start manual profiling for debugging
                    await Task.Delay(TimeSpan.FromSeconds(2f));
                }

                _logger.Trace("Profiler round ended");
            }
        }

        void OnProfilingFinished(IEnumerable<ProfilerRequest.Result> results)
        {
            var points = new List<PointData>();

            var topResults = results.Take(MaxGridCount).ToArray();
            foreach (var result in topResults)
            {
                var point = _dbClient.MakePointIn("profiler")
                    .Tag("grid_name", result.Name)
                    .Field("main_ms", result.MainThreadMsPerTick);

                points.Add(point);

                _logger.Trace($"point added: '{result.Name}' {result.MainThreadMsPerTick:0.00}");
            }

            _dbClient.WritePoints(points.ToArray());

            _logger.Trace($"Finished profiling & sending to DB; count: {topResults.Length}");
        }

        public void StopProfiling()
        {
            _running = false;
        }
    }
}