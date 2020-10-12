using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Profiler.Util
{
    public abstract class ObjectPool<T>
    {
        readonly List<T> _pooledObjects;

        protected ObjectPool()
        {
            _pooledObjects = new List<T>();
        }

        protected abstract T CreateNew();
        protected abstract void Reset(T obj);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public T UnpoolOrCreate()
        {
            if (_pooledObjects.Count == 0)
            {
                var newObj = CreateNew();
                _pooledObjects.Add(newObj);
                return newObj;
            }

            var lastIndex = _pooledObjects.Count - 1;
            var pooledObject = _pooledObjects[lastIndex];
            _pooledObjects.RemoveAt(lastIndex);
            return pooledObject;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Pool(T obj)
        {
            Reset(obj);
            _pooledObjects.Add(obj);
        }

        public void PoolAll(IEnumerable<T> objs)
        {
            foreach (var obj in objs)
            {
                Pool(obj);
            }
        }
    }
}