using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Profiler.Impl;
using Profiler.View;
using Torch;
using Torch.API;
using Torch.API.Plugins;

namespace Profiler
{
    /// <summary>
    /// Plugin that lets you profile entities 
    /// </summary>
    public class ProfilerPlugin : TorchPluginBase, IWpfPlugin
    {
        /// <inheritdoc cref="TorchPluginBase.Init"/>
        public override void Init(ITorchBase torch)
        {
            var pgmr = new ProfilerManager(torch);
            torch.Managers.AddManager(pgmr);
        }

        internal ProfilerPluginView _control;

        /// <inheritdoc cref="IWpfPlugin.GetControl"/>
        public UserControl GetControl() => _control = (_control ?? new ProfilerPluginView());
    }
}
