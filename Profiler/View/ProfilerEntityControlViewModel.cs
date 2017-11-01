using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Profiler.Impl;
using Torch.Server.ViewModels.Entities;

namespace Profiler.View
{
    public class ProfilerEntityControlViewModel : EntityControlViewModel
    {
        private ProfilerEntryViewModel _data;
        public ProfilerEntryViewModel Data
        {
            get => _data;
            internal set
            {
                if (_data == value)
                    return;
                if (_data != null)
                    _data.PropertyChanged -= DataOnPropertyChanged;
                _data = value;
                if (_data != null)
                    _data.PropertyChanged += DataOnPropertyChanged;
                OnPropertyChanged();
                UpdateVisiblity();
            }
        }

        private void DataOnPropertyChanged(object sender, PropertyChangedEventArgs arg)
        {
            if (arg.PropertyName.Equals(nameof(ProfilerEntryViewModel.UpdateTime)))
                UpdateVisiblity();
        }

        private void UpdateVisiblity()
        {
            Hide = _data == null || _data.UpdateTime <= 0;
        }
    }
}
