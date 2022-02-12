using System;
using System.Collections.Generic;
using Profiler.Core;
using Sandbox.Game.Entities;
using VRage.Game.Entity.EntityComponents.Interfaces;
using VRage.ModAPI;

namespace Profiler.Basics
{
    public sealed class EntityTypeProfiler : BaseProfiler<string>
    {
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
                default:
                {
                    // todo
                    return;
                }
            }
        }

        static string GetGameLogicComponentType(IMyGameLogicComponent logic)
        {
            var type = logic.GetType();
            return $"{type.Namespace}/{type.Name}";
        }
    }
}