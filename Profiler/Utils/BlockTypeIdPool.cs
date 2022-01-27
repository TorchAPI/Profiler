using System.Collections.Concurrent;
using Sandbox.Game.Entities;
using VRage.ObjectBuilders;

namespace Profiler.Utils
{
    public sealed class BlockTypeIdPool
    {
        public static readonly BlockTypeIdPool Instance = new();
        readonly ConcurrentDictionary<MyObjectBuilderType, string> _typeIds;

        BlockTypeIdPool()
        {
            _typeIds = new ConcurrentDictionary<MyObjectBuilderType, string>();
        }

        public string GetTypeId(MyObjectBuilderType type)
        {
            if (_typeIds.TryGetValue(type, out var s)) return s;

            var typeIdStr = type.ToString().Split('_')[1];
            _typeIds.TryAdd(type, typeIdStr);

            return typeIdStr;
        }
        
        // helpers

        public string GetTypeId(MyCubeBlock block)
        {
            return GetTypeId(block.BlockDefinition.Id.TypeId);
        }
    }
}