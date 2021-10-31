using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public class NesClockEventArgs : EventArgs
    {
        public NesClockEventArgs(ulong clockCycle)
        {
            ClockCycle = clockCycle;
        }

        public ulong ClockCycle { get; set; }
    }
}
