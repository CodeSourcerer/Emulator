using System;

namespace NESEmulator
{
    public interface Channel
    {
        void ClockQuarterFrame();
        void ClockHalfFrame();
        void Clock(ulong clockCycles);
        byte Read(ushort addr);
        void Write(ushort addr, byte data);
        short[] EmptyBuffer();
    }
}
