using Profiler.Core;
using Torch;
using Torch.API;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBase
    {
        /// <inheritdoc cref="TorchPluginBase.Init"/>
        public override void Init(ITorchBase torch)
        {
            base.Init(torch);

            var pgmr = new ProfilerManager(torch);
            torch.Managers.AddManager(pgmr);
        }
    }
}