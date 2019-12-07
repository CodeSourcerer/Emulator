using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public class APUSequencer
    {
        private ushort _timerReload;
        public ushort TimerReload
        {
            get => _timerReload;
            set
            {
                _timerReload = value;
            }
        }

        private ushort _timer;
        public ushort Timer
        {
            get => _timer;
            set
            {
                _timer = value;
            }
        }

        public event EventHandler OnTimerElapsed;

        public APUSequencer()
        {
        }

        public void Clock()
        {
            this.Timer--;

            if (this.Timer == 0xFFFF)
            {
                //_timerCallback();
                this.OnTimerElapsed?.Invoke(this, EventArgs.Empty);
                this.Timer = (ushort)(this.TimerReload + 1);
            }
        }
    }
}
