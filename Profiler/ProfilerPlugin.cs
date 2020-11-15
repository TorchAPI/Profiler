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
using TorchUtils;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBase
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
            this.ListenOnGameLoaded(() => OnGameLoaded());
            this.ListenOnGameUnloading(() => OnGameUnloading());

            var pgmr = new ProfilerManager(torch);
            torch.Managers.AddManager(pgmr);
        }

        void OnGameLoaded()
        {
            StartDbProfilers();

            MyMultiplayer.Static.Tick();
        }

        void StartDbProfilers()
        {
            // config
            const string ConfigFileName = "Profiler.config";
            if (!this.TryFindConfigFile<DbProfilerConfig>(ConfigFileName, out var config))
            {
                Log.Info("Creating a new DbProfiler config file with default content");
                this.CreateConfigFile(ConfigFileName, new DbProfilerConfig());
                this.TryFindConfigFile(ConfigFileName, out config);
            }

            _dbProfilers.AddRange(new IDbProfiler[]
            {
                new DbGameLoopProfiler(),
                new DbGridProfiler(),
                new DbFactionProfiler(),
                new DbBlockTypeProfiler(),
                new DbFactionGridProfiler(config),
                new DbMethodNameProfiler(),
            });

            _dbProfilersCanceller = new CancellationTokenSource();

            Task.Factory
                .StartNew(RunDbProfilers)
                .Forget(Log);
            
            Log.Info("database writing started");
        }

        void RunDbProfilers()
        {
            Parallel.ForEach(_dbProfilers, dbProfiler =>
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
                    Log.Warn(e);
                }
            });
        }

        void OnGameUnloading()
        {
            _dbProfilersCanceller.Cancel();
            _dbProfilersCanceller.Dispose();
        }
    }
}