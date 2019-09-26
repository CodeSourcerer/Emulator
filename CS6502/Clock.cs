using System;
namespace CS6502
{
    public delegate void ClockTickHandler(object sender, EventArgs e);

    public class Clock
    {
        long _systemClockCounter;

        public event ClockTickHandler OnClockTick;

        public Clock()
        {
        }
    }
}
