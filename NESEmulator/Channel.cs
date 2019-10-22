using System;

namespace NESEmulator
{
    public interface Channel
    {
        void ClockQuarterFrame();
        void ClockHalfFrame();
    }
}
