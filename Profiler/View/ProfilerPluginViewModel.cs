using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Profiler.Api;
using Profiler.Impl;
using Torch;
using Torch.API.Managers;
using Torch.API.Session;
using Torch.Collections;

namespace Profiler.View
{
    public class ProfilerPluginViewModel : ViewModel
    {
        /// <summary>
        /// Root nodes for profiler display.
        /// </summary>
        public MtObservableList<ProfilerEntryViewModel> ProfilerRoots { get; } = new MtObservableList<ProfilerEntryViewModel>();

        internal void RefreshContents(ProfilerManager mgr)
        {
            lock (ProfilerRoots)
            {
                while (ProfilerRoots.Count > (int) ProfilerFixedEntry.Count)
                    ProfilerRoots.RemoveAt((int)ProfilerFixedEntry.Count);
                for (var i = 0; i < (int) ProfilerFixedEntry.Count; i++)
                {
                    var entry = (ProfilerFixedEntry) i;
                    if (i < ProfilerRoots.Count)
                    {
                        var result = mgr.Fixed(entry, ProfilerRoots[i]);
                        // ReSharper disable once RedundantCheckBeforeAssignment
                        if (result != ProfilerRoots[i])
                            ProfilerRoots[i] = result;
                    }
                    else
                    {
                        ProfilerRoots.Add(mgr.Fixed(entry));
                    }
                }
            }
        }

        private ProfilerSettings _settings = null;

        public ProfilerSettings Settings
        {
            get => _settings;
            set
            {
                if (_settings == value)
                    return;
                _settings = value;
                OnPropertyChanged();
            }
        }

        public ProfilerPluginViewModel()
        {
            TorchBase.Instance.Managers.GetManager<ITorchSessionManager>().SessionStateChanged += MakeWeakDel(this);
            Refresh();
        }

        private static TorchSessionStateChangedDel MakeWeakDel(ProfilerPluginViewModel obj)
        {
            var weak = new WeakReference<ProfilerPluginViewModel>(obj);
            return (a, b) =>
            {
                if (weak.TryGetTarget(out ProfilerPluginViewModel ppvm))
                    ppvm.Refresh();
            };
        }

        private void Refresh()
        {
            var mgr = TorchBase.Instance.Managers.GetManager<ProfilerManager>();
            if (mgr == null)
            {
                ProfilerRoots.Clear();
                Settings = null;
                return;
            }

            Settings = mgr.Settings;
            RefreshContents(mgr);
        }
    }
}
