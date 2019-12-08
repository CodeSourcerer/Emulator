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
        public bool MuteChannel { get; private set; }
        public int DividerPeriod
        {
            get => _divider.CounterReload;
            set => _divider.CounterReload = value;
        }
        public byte ShiftCount { get; set; }
        public ushort ChannelPeriod { get; set; }

        public event EventHandler PeriodUpdate;

        private APUDivider _divider;
        private int _pulseChannelNumber;
        private int _targetPeriod;

        public APUSweep(int pulseChannelNumber)
        {
            this._pulseChannelNumber = pulseChannelNumber;
            this._divider = new APUDivider(APUDivider.DividerType.COUNTDOWN);
            this._divider.DividerReachedZero += divider_ReachedZero;
        }

        public void Clock()
        {
            calcTargetPeriod();

            if (this.Enabled)
            {
                this._divider.Clock();
            }

            if (this.Reload)
            {
                this._divider.Reset();
                this.Reload = false;
            }
        }

        private void divider_ReachedZero(object sender, EventArgs e)
        {
            if (this._targetPeriod <= MAX_TARGET_PERIOD && this.ShiftCount > 0 && this.MuteChannel == false)
            {
                this.ChannelPeriod = (ushort)this._targetPeriod;
                this.MuteChannel   = this.ChannelPeriod < 8;
                this.PeriodUpdate?.Invoke(this, EventArgs.Empty);
            }
        }

        private void calcTargetPeriod()
        {
            int shiftAmount = this.ChannelPeriod >> this.ShiftCount;

            if (this.Negate)
            {
                if (this._pulseChannelNumber == 1)
                    shiftAmount = ~shiftAmount; // take 1's compliment
                else
                    shiftAmount = -shiftAmount; // take 2's compliment
            }

            this._targetPeriod = this.ChannelPeriod + shiftAmount;
            this.MuteChannel = this._targetPeriod > MAX_TARGET_PERIOD;
        }
    }
}
