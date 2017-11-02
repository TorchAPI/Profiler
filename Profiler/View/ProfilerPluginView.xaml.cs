using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Profiler.Impl;
using Torch;
using Torch.API.Managers;

namespace Profiler.View
{
    /// <summary>
    /// Interaction logic for ProfilerPluginView.xaml
    /// </summary>
    public partial class ProfilerPluginView : UserControl
    {
        public ProfilerPluginView()
        {
            InitializeComponent();
            DataContext = new ProfilerPluginViewModel();
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            if (ReferenceEquals(sender, DataDump))
            {
                try
                {
                    TorchBase.Instance?.Managers.GetManager<ProfilerManager>()?.DumpToFile(
                        System.IO.Path.Combine(TorchBase.Instance.Config.InstancePath, "profiler_dump.xml"));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString(), "Error dumping profiler data");
                }
            }
        }
    }
}
