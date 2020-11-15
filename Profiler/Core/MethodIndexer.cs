﻿using System;
using System.Collections.Generic;

namespace Profiler.Core
{
    internal sealed class MethodIndexer
    {
        public static readonly MethodIndexer Instance = new MethodIndexer();

        readonly List<string> _mapping;

        MethodIndexer()
        {
            _mapping = new List<string>();
        }

        public int GetOrCreateIndexOf(Type type, string method)
        {
            return GetOrCreateIndexOf($"{type.FullName}#{method}");
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