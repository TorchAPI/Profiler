using System;
using System.Threading.Tasks;
using NLog;
using Profiler.Core;
using Sandbox.Engine.Multiplayer;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.Server.InfluxDb;
using TorchUtils.Utils;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBaseEx
    {
        static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        ProfilerDbClient _dbClient;

        /// <inheritdoc cref="TorchPluginBase.Init"/>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var pgmr = new ProfilerManager(torch);
            torch.Managers.AddManager(pgmr);
        }

        protected override void OnGameLoaded()
        {
            var dbManager = Torch.Managers.GetManager<InfluxDbManager>();
            if (dbManager == null)
            {
                throw new Exception($"{nameof(InfluxDbManager)} not found");
            }

            _dbClient = new ProfilerDbClient(dbManager.Client);

            StartThread(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(120));
                await _dbClient.StartProfiling();
            });

            MyMultiplayer.Static.Tick();
        }

        public void StartDbReporting()
        {
            StartThread(async () =>
            {
                await _dbClient.StartProfiling();
            });
        }

        public void StopDbReporting()
        {
            _dbClient.StopProfiling();
        }

        static void StartThread(Func<Task> f)
        {
            Task.Factory.StartNew(f).Forget(_logger);
        }
    }
}