using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using VRage;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.ObjectBuilders;

namespace Profiler.Impl
{
    public class ProfilerBlock
    {
        public string Name;

        [DefaultValue(0)]
        public long EntityId;

        [DefaultValue(0)]
        public long ParentEntityId;

        [DefaultValue(null)]
        public string ParentEntityName;

        [XmlElement("Owner")]
        public OwnershipData[] Owner;

        [XmlIgnore]
        private SerializableVector3D? _position;

        public SerializableVector3D Position
        {
            get => _position ?? default(SerializableVector3D);
            set => _position = value;
        }

        public bool ShouldSerializePosition()
        {
            return _position.HasValue;
        }

        [DefaultValue(0)]
        public int ChildCount
        {
            get => Children?.Length ?? 0;
            // ReSharper disable once ValueParameterNotUsed
            set { }
        }

        [XmlIgnore]
        private SerializableDefinitionId? _definition;

        public SerializableDefinitionId Definition
        {
            get => _definition ?? default(SerializableDefinitionId);
            set => _definition = value;
        }

        [DefaultValue(null)]
        public string VoxelStorage;

        public bool ShouldSerializeDefinition()
        {
            return _definition.HasValue;
        }

        public double TimeElapsed;
        
        public ProfilerBlock[] Children;

        public bool ShouldSerializeChildren()
        {
            return Children != null && Children.Length != 0;
        }

        internal void SetOwner(object obj)
        {
            Name = ProfilerObjectIdentifier.Identify(obj);
            if (obj is IMyEntity ent)
                SetEntity(ent);
            if (obj is MyEntityComponentBase cmp)
                SetEntity(cmp.Entity);
        }

        private void SetEntity(IMyEntity ent)
        {
            if (ent == null)
                return;
            EntityId = ent.EntityId;
            if (ent is IMyCubeBlock block)
            {
                var owner = MySession.Static?.Players.TryGetIdentity(block.OwnerId);
                if (owner != null)
                    Owner = new[] { new OwnershipData(owner) };
                _definition = block.BlockDefinition;
            }
            else if (ent is MyCubeGrid grid)
            {
                Owner = grid.BigOwners.Concat(grid.SmallOwners).Distinct()
                    .Select(x => MySession.Static?.Players.TryGetIdentity(x)).Where(x => x != null)
                    .Select(x => new OwnershipData(x)).ToArray();
            } else if (ent is MyPlanet planet)
            {
                _definition = planet.Generator.Id;
            }
            if (ent is MyVoxelBase vox)
                VoxelStorage = vox.StorageName;
            var parentData = ent.Components.Get<MyHierarchyComponentBase>();
            if (parentData?.Parent != null)
            {
                var parentEntity = parentData.Parent.Entity;
                ParentEntityId = parentEntity.EntityId;
                ParentEntityName = parentEntity.DisplayName;
            }
            var positionComp = ent.Components.Get<MyPositionComponentBase>();
            if (positionComp != null)
                _position = positionComp.GetPosition();
        }

        public class OwnershipData
        {
            [XmlAttribute(nameof(Name))]
            public string Name;

            [XmlAttribute(nameof(IdentityId))]
            public long IdentityId;

            [DefaultValue(0)]
            [XmlAttribute(nameof(SteamId))]
            public ulong SteamId;

            public OwnershipData()
            {
            }

            public OwnershipData(MyIdentity id)
            {
                IdentityId = id.IdentityId;
                SteamId = MySession.Static?.Players.TryGetSteamId(id.IdentityId) ?? 0;
                Name = id.DisplayName;
            }
        }
    }
}