using System;
using System.Collections.Generic;
using System.Threading;
using NLog;

namespace Profiler.Core
{
    internal sealed class FastConcurrentQueue<T>
    {
        readonly NullableArrayList<QueuePair<T>> _queues;

        public FastConcurrentQueue()
        {
            _queues = new NullableArrayList<QueuePair<T>>(100);
        }

        public bool TryDequeue(ref int index, out T element)
        {
            while (index < _queues.Length)
            {
                if (_queues[index] is { } queuePair &&
                    queuePair.TryDequeue(out element))
                {
                    return true;
                }

                index += 1;
            }

            element = default;
            return false;
        }

        public void Enqueue(in T result)
        {
            var index = Thread.CurrentThread.ManagedThreadId;

            if (!_queues.HasElementAt(index))
            {
                _queues.InsertElementAt(index, new QueuePair<T>());
            }

            _queues[index]?.Enqueue(result);
        }

        public void Alternate()
        {
            for (var i = 0; i < _queues.Length; i++)
            {
                _queues[i]?.Alternate();
            }
        }
    }

    internal sealed class QueuePair<T>
    {
        readonly Queue<T> _queue0;
        readonly Queue<T> _queue1;
        readonly bool _initialized;
        bool _switch;

        public QueuePair()
        {
            _queue0 = new Queue<T>();
            _queue1 = new Queue<T>();
            _initialized = true;
        }

        public void Alternate()
        {
            _switch = !_switch;
        }

        public void Enqueue(in T element)
        {
            if (!_initialized) return; //very rare

            var current = _switch ? _queue0 : _queue1;
            current.Enqueue(element);
        }

        public bool TryDequeue(out T element)
        {
            element = default;
            if (!_initialized) return false; //very rare

            var current = !_switch ? _queue0 : _queue1;
            return current.TryDequeue(out element);
        }
    }

    internal sealed class NullableArrayList<T> where T : class
    {
        readonly ILogger Log = LogManager.GetCurrentClassLogger();

        T[] _array;

        public NullableArrayList(int initialLength)
        {
            _array = new T[initialLength];
        }

        public T this[int index] => _array[index];
        public int Length => _array.Length;

        public bool HasElementAt(int index)
        {
            return index < _array.Length && _array[index] != null;
        }

        public void InsertElementAt(int index, T element)
        {
            if (index >= _array.Length)
            {
                Expand(index + 1);
            }

            _array[index] = element;
            Log.Debug($"INSERT {index}");
        }

        void Expand(int length)
        {
            var maxLength = Math.Max(length, _array.Length); // todo pick next 2^n
            var array = new T[maxLength];
            Array.Copy(_array, array, _array.Length);
            _array = array; // there's a shallow chance you miss something here

            Log.Debug($"EXPAND {length}");
        }
    }
}