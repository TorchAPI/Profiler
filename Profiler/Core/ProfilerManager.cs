using System;
using System.Threading;
using NLog;
using Torch.API;
using Torch.Managers;
using Torch.Managers.PatchManager;

namespace Profiler.Core
{
    public class ProfilerManager : Manager
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();
        CancellationTokenSource _canceller;

#pragma warning disable 649
        [Dependency(Ordered = false)]
        readonly PatchManager _patchMgr;
#pragma warning restore 649

        public ProfilerManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        static bool _patched;
        PatchContext _patchContext;

        /// <inheritdoc cref="Manager.Attach"/>
        public override void Attach()
        {
            base.Attach();
            if (!_patched)
            {
                _patched = true;
                _patchContext = _patchMgr.AcquireContext();
                ProfilerPatch.Patch(_patchContext);

                _canceller?.Cancel();
                _canceller?.Dispose();
                _canceller = new CancellationTokenSource();

                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    try
                    {
                        await ProfilerResultQueue.Start(_canceller.Token);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                });
            }
        }

        /// <inheritdoc cref="Manager.Detach"/>
        public override void Detach()
        {
            base.Detach();
            if (_patched)
            {
                _patched = false;
                _patchMgr.FreeContext(_patchContext);

                _canceller?.Cancel();
                _canceller?.Dispose();
                _canceller = null;
            }
        }
    }
}