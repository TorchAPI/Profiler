using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Navigation;
using Utils.Torch;

namespace Profiler
{
    public partial class ProfilerControl
    {
        readonly ProfilerPlugin _plugin;

        public ProfilerControl(ProfilerPlugin plugin)
        {
            _plugin = plugin;
            DataContext = ProfilerConfig.Instance;
            InitializeComponent();
        }

        public void OnConfigReloaded()
        {
            Dispatcher.Invoke(() =>
            {
                DataContext = ProfilerConfig.Instance;
                InitializeComponent();
            });
        }

        public void OnConfigChanged()
        {
            Dispatcher.Invoke(() =>
            {
                //
            });
        }

        void OnReloadClick(object sender, RoutedEventArgs e)
        {
            _plugin.ReloadConfig();
        }

        void RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        public static IEnumerable<Tuple<string, string, string>> GetAllCommands()
        {
            return CommandModuleUtils
                .GetCommandMethods(typeof(ProfilerCommands))
                .OrderByDescending(p => p.Permission)
                .Select(p => new Tuple<string, string, string>($"!{p.Command.Name}", p.Command.Description, p.Item2.ToReadableString()));
        }
    }
}