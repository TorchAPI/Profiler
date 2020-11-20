using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Profiler.TorchUtils
{
    //todo https://docs.microsoft.com/en-us/dotnet/standard/collections/thread-safe/how-to-create-an-object-pool
    
    /// <summary>
    /// Typical object pool with thread-safe pooling/unpooling functions.
    /// O(1) for pooling and unpooling.
    /// </summary>
    /// <typeparam name="T">Type of objects to be stored in this pool.</typeparam>
    public abstract class ObjectPool<T>
    {
        readonly Queue<T> _pooledObjects;
        int _objectCount;

        protected ObjectPool()
        {
            _pooledObjects = new Queue<T>();
        }

        /// <summary>
        /// Create a new object. Called when unpooling is requested when the pool is empty.
        /// </summary>
        /// <returns>New object.</returns>
        protected abstract T CreateNew();
        
        /// <summary>
        /// Initialize given object. Called when an object is pooled.
        /// </summary>
        /// <param name="obj"></param>
        protected abstract void Reset(T obj);

        /// <summary>
        /// Unpool an object.
        /// </summary>
        /// <remarks>New object will be instantiated if no objects are present in the pool.</remarks>
        /// <returns>An unpooled object or new object if the pool is empty.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public T UnpoolOrCreate()
        {
            if (_objectCount == 0)
            {
                return CreateNew();
            }

            var pooledObject = _pooledObjects.Dequeue();
            _objectCount -= 1;
            return pooledObject;
        }

        /// <summary>
        /// Pool an object.
        /// </summary>
        /// <param name="obj">Object to pool.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Pool(T obj)
        {
            Reset(obj);
            _pooledObjects.Enqueue(obj);
            _objectCount += 1;
        }

        /// <summary>
        /// Pool all objects in given collection.
        /// </summary>
        /// <param name="objects">Collection of objects to be pooled.</param>
        public void PoolAll(IEnumerable<T> objects)
        {
            foreach (var obj in objects)
            {
                Pool(obj);
            }
        }
    }
}