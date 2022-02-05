using System;
using System.Threading;
using NLog;
using Profiler.Core;
using Torch;
using Torch.API;
using Torch.API.Session;
using Torch.Session;
using Torch.API.Managers;
using Torch.Managers.PatchManager;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBase
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        PatchManager _patchManager;
        PatchContext _patchContext;
        CancellationTokenSource _canceller;

        /// <inheritdoc cref="TorchPluginBase.Init"/>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var sessionManager = Torch.Managers.GetManager<TorchSessionManager>();
            sessionManager.SessionStateChanged += (_, state) =>
            {
                switch (state)
                {
                    case TorchSessionState.Loaded:
                    {
                        OnGameLoaded();
                        return;
                    }
                    case TorchSessionState.Unloading:
                    {
                        OnGameUnloading();
                        return;
                    }
                    default:
                    {
                        return;
                    }
                }
            };
        }

        void OnGameLoaded()
        {
            _patchManager = Torch.Managers.GetManager<PatchManager>();
            _patchContext = _patchManager.AcquireContext();

            ProfilerPatch.Patch(_patchContext);
            _patchManager.Commit();

            _canceller?.Cancel();
            _canceller?.Dispose();
            _canceller = new CancellationTokenSource();

            StartQueue();
        }

        async void StartQueue()
        {
            try
            {
                await ProfilerResultQueue.Start(_canceller.Token);
            }
            catch (OperationCanceledException)
            {
                //pass
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        void OnGameUnloading()
        {
            _canceller?.Cancel();
            _canceller?.Dispose();
            _canceller = null;

            if (_patchManager != null && _patchContext != null)
            {
                _patchManager.FreeContext(_patchContext);
            }
        }
    }
}