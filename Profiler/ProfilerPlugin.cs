using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using Profiler.Core;
using Profiler.Database;
using Sandbox.Engine.Multiplayer;
using Torch;
using Torch.API;
using Torch.API.Managers;
using Torch.Server.InfluxDb;
using Torch.Server.Utils;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBaseEx
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        readonly List<IDbProfiler> _dbProfilers;
        CancellationTokenSource _dbProfilersCanceller;

        public ProfilerPlugin()
        {
            _dbProfilers = new List<IDbProfiler>();
        }

        /// <inheritdoc cref="TorchPluginBase.Init"/>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var pgmr = new ProfilerManager(torch);
            torch.Managers.AddManager(pgmr);
        }

        protected override void OnGameLoaded()
        {
            Task.Factory.StartNew(StartDbProfilers).Forget(Log);

            MyMultiplayer.Static.Tick();
        }

        void StartDbProfilers()
        {
            // config
            const string ConfigFileName = "Profiler.config";
            if (!TryFindConfigFile<DbProfilerConfig>(ConfigFileName, out var config))
            {
                Log.Info("Creating a new DbProfiler config file with default content");
                CreateConfigFile(ConfigFileName, new DbProfilerConfig());
                TryFindConfigFile(ConfigFileName, out config);
            }

            // database endpoint
            var dbManager = Torch.Managers.GetManager<InfluxDbManager>();
            if (dbManager == null)
            {
                throw new Exception($"{nameof(InfluxDbManager)} not found");
            }

            _dbProfilers.AddRange(new IDbProfiler[]
            {
                new DbTotalProfiler(dbManager.Client),
                new DbGridProfiler(dbManager.Client),
                new DbFactionProfiler(dbManager.Client),
                new DbBlockTypeProfiler(dbManager.Client),
                new DbFactionGridProfiler(dbManager.Client, config),
            });

            _dbProfilersCanceller = new CancellationTokenSource();
            foreach (var dbProfiler in _dbProfilers)
            {
                try
                {
                    dbProfiler.StartProfiling(_dbProfilersCanceller.Token);
                }
                catch (OperationCanceledException)
                {
                    // pass
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        protected override void OnGameUnloading()
        {
            _dbProfilersCanceller.Cancel();
            _dbProfilersCanceller.Dispose();
        }
    }
}