using VRage.ModAPI;

namespace Profiler.Util
{
    public static class ProfilerUtils
    {
        public static T GetParentEntityOfType<T>(this IMyEntity ent) where T : class, IMyEntity
        {
            while (ent != null)
            {
                if (ent is T match) return match;
                ent = ent.Parent;
            }

            return null;
        }
    }
}