using System.Reflection;
using System.Runtime.CompilerServices;
using NLog;
using Profiler.Core.Patches;
using Profiler.Utils;
using Torch.Managers.PatchManager;
using Torch.Utils;
using Torch.Utils.Reflected;

namespace Profiler.Core
{
    [ReflectedLazy]
    internal static class ProfilerPatch
    {
        static readonly Logger Log = LogManager.GetCurrentClassLogger();

        public static readonly MethodInfo StopTokenFunc = typeof(ProfilerPatch).GetStaticMethod(nameof(StopToken));
        public static bool Enabled { get; set; } = true;

        public static void Patch(PatchContext ctx)
        {
            Log.Trace("Profiler patch started");

            ReflectedManager.Process(typeof(ProfilerPatch));

            // MyEntity and MyEntityComponentBase
            MyGameLogic_Update.Patch(ctx);
            MyParallelEntityUpdateOrchestrator_Transpile.Patch(ctx);
            MyProgrammableBlock_RunSandboxedProgramAction.Patch(ctx);

            // Game loop in call hierarchy
            Game_RunSingleFrame.Patch(ctx);
            {
                Game_UpdateInternal.Patch(ctx);
                {
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
                    {
                        MySession_Update_Transpile.Patch(ctx);
                        MyReplicationServer_UpdateBefore.Patch(ctx);
                        MySession_UpdateComponents.Patch(ctx);
                        {
                            MySession_UpdateComponents_Transpile.Patch(ctx);
                        }
                        MyGpsCollection_Update.Patch(ctx);
                        MyReplicationServer_UpdateAfter.Patch(ctx);
                        MyDedicatedServer_Tick.Patch(ctx);
                        {
                            MyReplicationServer_SendUpdate.Patch(ctx);
                            
                        }
                        MyPlayerCollection_SendDirtyBlockLimits.Patch(ctx);
                    }
                }
                FixedLoop_Run.Patch(ctx);
            }

            Log.Trace("Profiler patch ended");
        }

        // ReSharper disable once RedundantAssignment
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ProfilerToken? StartToken(object caller, int methodIndex, ProfilerCategory category)
        {
            if (!Enabled) return null;
            return new ProfilerToken(caller, methodIndex, category);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StopToken(in ProfilerToken? tokenOrNull)
        {
            if (!Enabled) return;
            if (!(tokenOrNull is ProfilerToken token)) return;

            var result = new ProfilerResult(token);
            ProfilerResultQueue.Enqueue(result);
        }
    }
}