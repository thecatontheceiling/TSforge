namespace LibTSforge.PhysicalStore
{
    using System.Runtime.InteropServices;

    public enum BlockType : uint
    {
        NONE,
        NAMED,
        ATTRIBUTE,
        TIMER
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Timer
    {
        public ulong Unknown;
        public ulong Time1;
        public ulong Time2;
        public ulong Expiry;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct VistaTimer
    {
        public ulong Time;
        public ulong Expiry;
    }
}
