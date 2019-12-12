using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.APU
{
    public class APUSequencer
    {
        public ushort TimerReload { get; set; }

        public ushort Timer { get; set; }

        public event EventHandler OnTimerElapsed;

        public APUSequencer()
        {
        }

        public void Clock()
        {
            if (Timer == 0)
            {
                Timer = TimerReload;
                OnTimerElapsed?.Invoke(this, EventArgs.Empty);
            }
            else
                Timer--;
        }
    }
}
