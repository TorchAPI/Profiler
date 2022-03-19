using System;
using System.Threading;
using NLog;
using Profiler.Core;
using Profiler.Core.Patches;
using Torch;
using Torch.API;
using Torch.API.Session;
using Torch.Session;
using Torch.API.Managers;
using Torch.Managers.PatchManager;
using Torch.Utils;
using VRageMath;
using VRageMath.Spatial;

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

            Log.Info("Profile patch start");
            Patch(_patchContext);
            _patchManager.Commit();
            Log.Info("Profile patch done");

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
        
        static void Patch(PatchContext ctx)
        {
            Log.Trace("Profiler patch started");

            ReflectedManager.Process(typeof(ProfilerPatch));

            // Game loop in call hierarchy
            Game_RunSingleFrame.Patch(ctx);
            {
                Game_UpdateInternal.Patch(ctx);
                { // MySandboxGame.Update() to MySession.Update()
                    MyTransportLayer_Tick.Patch(ctx);
                    MyGameService_Update.Patch(ctx);
                    MyNetworkReader_Process.Patch(ctx);
                    {
                        MyDedicatedServerBase_ClientConnected.Patch(ctx);
                        MyMultiplayerServerBase_ClientReady.Patch(ctx);
                        MyReplicationServer_OnClientAcks.Patch(ctx);
                        MyReplicationServer_OnClientUpdate.Patch(ctx);
                        MyReplicationServer_ReplicableReady.Patch(ctx);
                        MyReplicationServer_ReplicableRequest.Patch(ctx);
                        MyReplicationServer_OnEvent.Patch(ctx);
                    }
                    MyDedicatedServer_ReportReplicatedObjects.Patch(ctx);
                    MySession_Update_Transpile.Patch(ctx);
                    MyReplicationServer_UpdateBefore.Patch(ctx);
                    MySession_UpdateComponents_Transpile.Patch(ctx); // session components
                    {
                        // MyEntity and MyEntityComponentBase
                        MyGameLogic_Update.Patch(ctx);
                        MyParallelEntityUpdateOrchestrator_Transpile.Patch(ctx);
                        MyUpdateOrchestrator_Transpile.Patch(ctx);
                        MyProgrammableBlock_RunSandboxedProgramAction.Patch(ctx);

                        // Physics
                        MyPhysics_Simulate.Patch(ctx);
                        MyPhysics_StepWorlds.Patch(ctx);
                    }
                    MyGpsCollection_Update.Patch(ctx);
                    MyReplicationServer_UpdateAfter.Patch(ctx);
                    MyDedicatedServer_Tick.Patch(ctx);
                    {
                        MyReplicationServer_SendUpdate.Patch(ctx);
                    }
                    MyPlayerCollection_SendDirtyBlockLimits.Patch(ctx);
                }
                FixedLoop_Run.Patch(ctx);
            }

            Log.Trace("Profiler patch ended");
        }
    }
}