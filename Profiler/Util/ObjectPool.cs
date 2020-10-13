using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Profiler.Util
{
    public abstract class ObjectPool<T>
    {
        readonly Queue<T> _pooledObjects;

        protected ObjectPool()
        {
            _pooledObjects = new Queue<T>();
        }

        protected abstract T CreateNew();
        protected abstract void Reset(T obj);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public T UnpoolOrCreate()
        {
            if (_pooledObjects.Count == 0)
            {
                return CreateNew();
            }

            var pooledObject = _pooledObjects.Dequeue();
            return pooledObject;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Pool(T obj)
        {
            Reset(obj);
            _pooledObjects.Enqueue(obj);
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