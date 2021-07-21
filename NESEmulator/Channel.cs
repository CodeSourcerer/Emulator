using System;

namespace NESEmulator
{
    public interface Channel
    {
        void ClockQuarterFrame(ulong cpuCycle);
        void ClockHalfFrame(ulong cpuCycle);
        void Clock(ulong clockCycles);
        byte Read(ushort addr);
        void Write(ushort addr, byte data);

        short Output { get; }
        bool Enabled { get; set; }
    }
}
