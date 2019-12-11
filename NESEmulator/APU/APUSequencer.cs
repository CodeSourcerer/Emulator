using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.APU
{
    public class APUSequencer
    {
        public ushort TimerReload { get; set; }
        public ushort Timer { get; set; }

        private EventHandler _timerElapsed;

        public APUSequencer(EventHandler callback)
        {
            _timerElapsed = callback;
        }

        public void Clock()
        {
            if (Timer == 0xFFFF)
            {
                Timer = TimerReload;
                _timerElapsed(this, EventArgs.Empty);
                //this.OnTimerElapsed?.Invoke(this, EventArgs.Empty);
            }
            else
                Timer--;
        }
    }
}
