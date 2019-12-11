using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.APU
{
    /// <summary>
    /// Represents the pulse channel sweep unit
    /// </summary>
    public class APUSweep
    {
        private const int MAX_TARGET_PERIOD = 0x7FF;

        public bool Enabled { get; set; }
        public bool Reload { get; set; }
        public bool Negate { get; set; }
        public bool MuteChannel { get; set; }
        public int DividerPeriod
        {
            get => _divider.CounterReload;
            set => _divider.CounterReload = value;
        }
        public byte ShiftCount { get; set; }
        public ushort ChannelPeriod { get; private set; }

        //public event EventHandler PeriodUpdate;
        private EventHandler _periodUpdate;

        private APUDivider _divider;
        private int _pulseChannelNumber;
        private int _targetPeriod;

        public APUSweep(int pulseChannelNumber, EventHandler callback)
        {
            _pulseChannelNumber = pulseChannelNumber;
            _periodUpdate = callback;
            _divider = new APUDivider(APUDivider.DividerType.COUNTDOWN, divider_ReachedZero);
            //this._divider.DividerReachedZero += divider_ReachedZero;
        }

        public void Clock()
        {
            calcTargetPeriod();

            _divider.Clock();

        }

        private void divider_ReachedZero(object sender, EventArgs e)
        {
            if (!MuteChannel && Enabled)
            {
                ChannelPeriod = (ushort)_targetPeriod;
                //this.MuteChannel   = shouldMuteChannel();
                _periodUpdate(this, EventArgs.Empty);
            }

            if (Reload)
            {
                Reload = false;
                _divider.Reset();
            }
        }

        private void calcTargetPeriod()
        {
            int shiftAmount = ChannelPeriod >> ShiftCount;

            if (Negate)
            {
                if (_pulseChannelNumber == 1)
                    shiftAmount = ~shiftAmount; // take 1's compliment
                else
                    shiftAmount = -shiftAmount; // take 2's compliment
            }

            _targetPeriod = ChannelPeriod + shiftAmount;
            if (_targetPeriod > MAX_TARGET_PERIOD)
                MuteChannel = true;
        }

        //private bool shouldMuteChannel() => _targetPeriod > MAX_TARGET_PERIOD || ChannelPeriod < 8;
    }
}
