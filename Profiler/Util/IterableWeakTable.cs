using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Profiler.Util
{
    public class IterableWeakTable<TKey, TVal> where TKey : class where TVal : class
    {
        private readonly ConditionalWeakTable<TKey, TVal> _table = new ConditionalWeakTable<TKey, TVal>();
        private readonly HashSet<TKey> _activeKeys = new HashSet<TKey>();
        private readonly HashSet<TKey> _cleanCache = new HashSet<TKey>();

        public TVal GetOrCreate(TKey key, ConditionalWeakTable<TKey, TVal>.CreateValueCallback creator)
        {
            var res = _table.GetValue(key, creator);
            _activeKeys.Add(key);
            return res;
        }

        public void Clean()
        {
            foreach (var key in _activeKeys)
                if (!_table.TryGetValue(key, out var tmp))
                    _cleanCache.Add(key);
            foreach (var key in _cleanCache)
                _activeKeys.Remove(key);
            _cleanCache.Clear();
        }
    }
}