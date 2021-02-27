using System;
using System.Collections.Generic;

namespace Profiler.Core
{
    /// <summary>
    /// Keeps string objects alive across patching.
    /// </summary>
    internal sealed class StringIndexer
    {
        public static readonly StringIndexer Instance = new StringIndexer();

        readonly List<string> _mapping;

        StringIndexer()
        {
            _mapping = new List<string>();
        }

        public int IndexOf(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                throw new Exception("method name null");
            }

            var existingIndex = _mapping.IndexOf(methodName);
            if (existingIndex >= 0) return existingIndex;

            _mapping.Add(methodName);
            return _mapping.Count - 1;
        }

        public string StringAt(int index)
        {
            if (index >= _mapping.Count)
            {
                throw new IndexOutOfRangeException($"length: {_mapping.Count}, given index: {index}");
            }

            return _mapping[index];
        }
    }
}