using System;
using Profiler.Core;
using Sandbox.Game.Entities;
using VRage.Game.Entity.EntityComponents.Interfaces;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class EntityTypeProfiler : BaseProfiler<string>
    {
        protected override bool TryAccept(in ProfilerResult profilerResult, out string key)
        {
            key = null;

            if (profilerResult.Category != ProfilerCategory.General) return false;
            switch (profilerResult.GameEntity)
            {
                case IMyEntity entity:
                {
                    key = GetEntityType(entity);
                    return true;
                }
                case IMyGameLogicComponent logic:
                {
                    key = GetGameLogicComponentType(logic);
                    return true;
                }
                default:
                {
                    return false;
                }
            }
        }

        static string GetEntityType(IMyEntity entity)
        {
            if (entity is MyCubeBlock)
            {
                return nameof(MyCubeBlock);
            }

            return entity.GetType().Name;
        }

        static string GetGameLogicComponentType(IMyGameLogicComponent logic)
        {
            var type = logic.GetType();
            return $"{type.Namespace}/{type.Name}";
        }
    }
}