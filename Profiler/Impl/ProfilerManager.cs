using System;
using System.ComponentModel;
using Profiler.Api;
using Profiler.View;
using Sandbox.Game.Entities;
using Torch.API;
using Torch.Managers;
using Torch.Managers.PatchManager;
using Torch.Server.Managers;
using Torch.Server.ViewModels.Entities;
using VRage.Game.Components;
using VRage.ModAPI;

namespace Profiler.Impl
{
    public class ProfilerManager : Manager
    {
#pragma warning disable 649
        [Dependency(Ordered = false)] private readonly PatchManager _patchMgr;

        [Dependency(Optional = true)] private readonly EntityControlManager _controlMgr;
#pragma warning restore 649

        public ProfilerManager(ITorchBase torchInstance) : base(torchInstance)
        {
        }

        private static bool _patched = false;
        private PatchContext _patchContext;

        /// <inheritdoc cref="Manager.Attach"/>
        public override void Attach()
        {
            base.Attach();
            if (!_patched)
            {
                _patched = true;
                _patchContext = _patchMgr.AcquireContext();
                ProfilerPatch.Patch(_patchContext);
            }

            _controlMgr?.RegisterControlFactory<ProfilerEntityControlViewModel>(CreateView);
            _controlMgr?.RegisterModelFactory<EntityViewModel>(CreateModel);
        }

        /// <inheritdoc cref="Manager.Detach"/>
        public override void Detach()
        {
            base.Detach();
            if (_patched)
            {
                _patched = false;
                _patchMgr.FreeContext(_patchContext);
            }

            _controlMgr?.UnregisterModelFactory<EntityViewModel>(CreateModel);
            _controlMgr?.UnregisterControlFactory<ProfilerEntityControlViewModel>(CreateView);
        }

        // Extracted to ensure we don't capture anything except the weak ref and the profiler manager.
        private PropertyChangedEventHandler TreeViewUpdater(WeakReference<ProfilerEntityControlViewModel> model)
        {
            const string entityProp = nameof(EntityViewModel.Entity);
            return (sender, args) =>
            {
                if (sender is EntityViewModel gvm &&
                    args.PropertyName == entityProp && model.TryGetTarget(out ProfilerEntityControlViewModel tree))
                {
                    tree.Data = EntityData(gvm.Entity, tree.Data);
                }
            };
        }

        private ProfilerEntityControlViewModel CreateModel(EntityViewModel gvm)
        {
            var ptvm = new ProfilerEntityControlViewModel();
            var updater = TreeViewUpdater(new WeakReference<ProfilerEntityControlViewModel>(ptvm));
            gvm.PropertyChanged += updater;
            updater(gvm, new PropertyChangedEventArgs(nameof(EntityViewModel.Entity)));
            return ptvm;
        }

        private ProfilerEntityControl CreateView(ProfilerEntityControlViewModel model)
        {
            return new ProfilerEntityControl() {DataContext = model};
        }

        /// <summary>
        /// Gets the settings associated with this profiler.
        /// </summary>
        public ProfilerSettings Settings { get; } = new ProfilerSettings();

        /// <summary>
        /// Gets the profiler information associated with the given entity.
        /// </summary>
        /// <param name="entity">Entity to get information for</param>
        /// <param name="cache">View model to reuse, or null to create a new one</param>
        /// <returns>Information</returns>
        public ProfilerEntryViewModel EntityData(IMyEntity entity, ProfilerEntryViewModel cache = null)
        {
            cache = ProfilerData.BindView(cache);
            cache.SetTarget(entity);
            return cache;
        }


        /// <summary>
        /// Gets the profiler information associated with the given cube grid system.
        /// </summary>
        /// <param name="grid">Cube grid to query</param>
        /// <param name="cubeGridSystem">Cube grid system to query</param>
        /// <param name="cache">View model to reuse, or null to create a new one</param>
        /// <returns>Information</returns>
        public ProfilerEntryViewModel GridSystemData(MyCubeGrid grid, object cubeGridSystem,
            ProfilerEntryViewModel cache = null)
        {
            cache = ProfilerData.BindView(cache);
            cache.SetTarget(grid, cubeGridSystem);
            return cache;
        }


        /// <summary>
        /// Gets the profiler information associated with the given entity component
        /// </summary>
        /// <param name="component">Component to get information for</param>
        /// <param name="cache">View model to reuse, or null to create a new one</param>
        /// <returns>Information</returns>
        public ProfilerEntryViewModel EntityComponentData(MyEntityComponentBase component,
            ProfilerEntryViewModel cache = null)
        {
            cache = ProfilerData.BindView(cache);
            cache.SetTarget(component);
            return cache;
        }

        /// <summary>
        /// Gets profiler root.
        /// </summary>
        ///<param name="entry">The root</param>
        /// <returns>View model</returns>
        public ProfilerEntryViewModel Fixed(ProfilerFixedEntry entry, ProfilerEntryViewModel cache = null)
        {
            cache = ProfilerData.BindView(cache);
            cache.SetTarget(entry);
            return cache;

        }
    }
}
