using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;
using VRage.Game.Entity.EntityComponents.Interfaces;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class EntityTypeProfiler : BaseProfiler<string>
    {
        readonly ConcurrentDictionary<Type, string> _names;

        public EntityTypeProfiler()
        {
            _names = new ConcurrentDictionary<Type, string>();
        }

        protected override void Accept(in ProfilerResult profilerResult, ICollection<string> acceptedKeys)
        {
            if (profilerResult.Category != ProfilerCategory.General) return;

            switch (profilerResult.GameEntity)
            {
                case MyCubeBlock:
                {
                    acceptedKeys.Add(nameof(MyCubeBlock)); // don't go down too deep
                    return;
                }
                case IMyEntity entity:
                {
                    var key = entity.GetType().Name;
                    acceptedKeys.Add(key);
                    return;
                }
                case IMyGameLogicComponent logic:
                {
                    var key = GetGameLogicComponentType(logic);
                    acceptedKeys.Add(key);
                    return;
                }
                case null:
                {
                    throw new InvalidOperationException("null in the profiler; possibly patch overlap by other plugins");
                }
                default:
                {
                    throw new InvalidOperationException($"shouldn't happen: {profilerResult.GameEntity.GetType()}");
                }
            }
        }

        string GetGameLogicComponentType(IMyGameLogicComponent logic)
        {
            var type = logic.GetType();
            if (!_names.TryGetValue(type, out var name))
            {
                _names[type] = name = $"{type.Namespace}/{type.Name}";
            }

            return name;
        }
    }
}