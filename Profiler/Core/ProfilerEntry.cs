namespace Profiler.Core
{
    public sealed class ProfilerEntry
    {
        public ulong LastResetTick;
        
        public long MainThreadUpdates;
        public long MainThreadTime;

        public long OffThreadUpdates;
        public long OffThreadTime;

        public void Reset(ulong tick)
        {
            LastResetTick = tick;
            
            MainThreadTime = 0;
            MainThreadUpdates = 0;

            OffThreadUpdates = 0;
            OffThreadTime = 0;
        }
    }
}