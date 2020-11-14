using Profiler.Core.Patches;

namespace Profiler.Core
{
    public static class ProfilerCategory
    {
        public const string General = "General";
        public const string Scripts = "Scripts";
        public const string Total = Game_UpdateInternal.Category;
    }
}