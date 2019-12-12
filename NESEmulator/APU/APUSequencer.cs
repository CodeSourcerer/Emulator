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
            if (this.Timer == 0)
            {
                this.Timer = TimerReload;
                //_timerCallback();
                this.OnTimerElapsed?.Invoke(this, EventArgs.Empty);
            }
            else
                Timer--;
        }
    }
}
