using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.APU
{
    /// <summary>
    /// The sequencer generates a frequency by triggering a handler when a timer reaches 0.
    /// The timer reload value determines the frequency.
    /// </summary>
    public class APUSequencer
    {
        private ushort _timerReload;
        public ushort TimerReload
        {
            get => _timerReload;
            set
            {
                _timerReload = value;
                Timer = value;
            }
        }

        public ushort Timer { get; set; }

        //public event EventHandler OnTimerElapsed;

        private EventHandler _callback;

        public APUSequencer(EventHandler sequencerCallback)
        {
            _callback = sequencerCallback;
        }

        public void Clock()
        {
            if (Timer == 0)
            {
                Timer = TimerReload;
                //OnTimerElapsed?.Invoke(this, EventArgs.Empty);
                _callback(this, EventArgs.Empty);
            }
            else
                Timer--;
        }
    }
}
