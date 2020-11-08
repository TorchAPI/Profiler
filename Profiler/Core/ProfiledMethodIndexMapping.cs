using System;
using System.Collections.Generic;

namespace Profiler.Core
{
    internal sealed class ProfiledMethodIndexMapping
    {
        public static readonly ProfiledMethodIndexMapping Instance = new ProfiledMethodIndexMapping();

        readonly List<string> _mapping;

        ProfiledMethodIndexMapping()
        {
            _mapping = new List<string>();
        }

        public int GetOrCreateIndexOf(string methodName)
        {
            var existingIndex = _mapping.IndexOf(methodName);
            if (existingIndex >= 0) return existingIndex;

            _mapping.Add(methodName);
            return _mapping.Count - 1;
        }

        public string GetMethodNameOf(int index)
        {
            if (index >= _mapping.Count)
            {
                throw new IndexOutOfRangeException($"length: {_mapping.Count}, given index: {index}");
            }

            return _mapping[index];
        }
    }
}