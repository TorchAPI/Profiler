using System;
using System.Collections.Generic;

namespace Profiler.Interactive
{
    public sealed class PhysicsParamParser
    {
        public PhysicsParamParser(IEnumerable<string> args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("--inspect="))
                {
                    var indexStr = arg.Split('=')[1];
                    if (!int.TryParse(indexStr, out var inspectIndex))
                    {
                        throw new ArgumentException($"not a number: \"{indexStr}\"");
                    }

                    InspectIndexOrNull = inspectIndex;
                    continue;
                }

                if (arg == "--takeme=done")
                {
                    TakeMeDone = true;
                    continue;
                }

                if (arg.StartsWith("--takeme="))
                {
                    var indexStr = arg.Split('=')[1];
                    if (!int.TryParse(indexStr, out var takeMeIndex))
                    {
                        throw new ArgumentException($"not a number: \"{indexStr}\"");
                    }

                    TakeMeIndexOrNull = takeMeIndex;
                    continue;
                }

                if (arg.StartsWith("--tics="))
                {
                    var ticsStr = arg.Split('=')[1];
                    if (!int.TryParse(ticsStr, out var tics))
                    {
                        throw new ArgumentException($"not a number: \"{ticsStr}\"");
                    }

                    Tics = tics;
                    continue;
                }
            }
        }

        public int Tics { get; } = 10;
        public int? InspectIndexOrNull { get; }
        public int? TakeMeIndexOrNull { get; }
        public bool TakeMeDone { get; }
    }
}