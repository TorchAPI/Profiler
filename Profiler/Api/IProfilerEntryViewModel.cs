using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Profiler.Impl;
using Torch.Collections;
using VRage.Game;

namespace Profiler.Api
{
    public interface IProfilerEntryViewModel : INotifyPropertyChanged
    {
        string OwnerName { get; }
        double UpdateTime { get; }
        MtObservableList<ProfilerEntryViewModel> Children { get; }
        MtObservableList<ProfilerEntryViewModel> ChildrenSorted { get; }
    }
}
