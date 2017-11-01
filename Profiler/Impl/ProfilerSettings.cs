using System.ComponentModel;
using System.Runtime.CompilerServices;
using Torch.Collections;

namespace Profiler.Impl
{
    public class ProfilerSettings : INotifyPropertyChanged
    {
        /// <summary>
        /// Display load percentage instead of time.
        /// </summary>
        public bool DisplayLoadPercentage
        {
            get => ProfilerData.DisplayLoadPercentage;
            set
            {
                if (ProfilerData.DisplayLoadPercentage == value)
                    return;
                ProfilerData.DisplayLoadPercentage = value;
                OnPropertyChanged();
                ProfilerData.ForcePropertyUpdate();
            }
        }

        /// <summary>
        /// Display mod names when applicable
        /// </summary>
        public bool DisplayModNames
        {
            get => ProfilerData.DisplayModNames;
            set
            {
                if (ProfilerData.DisplayModNames == value)
                    return;
                ProfilerData.DisplayModNames = value;
                OnPropertyChanged();
                ProfilerData.ForcePropertyUpdate();
            }
        }

        /// <summary>
        /// Profile grid related updates.
        /// </summary>
        public bool ProfileGridsUpdate
        {
            get => ProfilerData.ProfileGridsUpdate;
            set
            {
                ProfilerData.ProfileGridsUpdate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Profile block updates.  Requires <see cref="ProfileGridsUpdate"/>
        /// </summary>
        public bool ProfileBlocksUpdate
        {
            get => ProfilerData.ProfileBlocksUpdate;
            set
            {
                ProfilerData.ProfileBlocksUpdate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Profile entity component updates.
        /// </summary>
        public bool ProfileEntityComponentsUpdate
        {
            get => ProfilerData.ProfileEntityComponentsUpdate;
            set
            {
                ProfilerData.ProfileEntityComponentsUpdate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Profile grid system updates.  Requires <see cref="ProfileGridsUpdate"/>
        /// </summary>
        public bool ProfileGridSystemUpdates
        {
            get => ProfilerData.ProfileGridSystemUpdates;
            set
            {
                ProfilerData.ProfileGridSystemUpdates = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Profile session component updates.
        /// </summary>
        public bool ProfileSessionComponentsUpdate
        {
            get => ProfilerData.ProfileSessionComponentsUpdate;
            set
            {
                ProfilerData.ProfileSessionComponentsUpdate = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Profile individual methods.
        /// </summary>
        public bool ProfileSingleMethods
        {
            get => ProfilerData.ProfileSingleMethods;
            set
            {
                ProfilerData.ProfileSingleMethods = value;
                OnPropertyChanged();
            }
        }


        private readonly MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler> _propertyChangedEvent =
            new MtObservableEvent<PropertyChangedEventArgs, PropertyChangedEventHandler>();

        /// <inheritdoc/>
        public event PropertyChangedEventHandler PropertyChanged
        {
            add => _propertyChangedEvent.Add(value);
            remove => _propertyChangedEvent.Remove(value);
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            _propertyChangedEvent.Raise(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
