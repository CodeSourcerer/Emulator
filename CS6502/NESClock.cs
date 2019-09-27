using System;
namespace CS6502
{
    public delegate void ClockTickHandler(object sender, EventArgs e);

    public class NESClock
    {
        private long _systemClockCounter;

        public event ClockTickHandler OnClockTick;

        public NESClock()
        {

        }
    }
}
