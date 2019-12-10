using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace NESEmulator.APU
{
    /// <summary>
    /// Represents the pulse channel sweep unit
    /// </summary>
    public class APUSweep
    {
        private static ILog Log = LogManager.GetLogger(typeof(APUSweep));

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
        private byte _shiftCount;
        public byte ShiftCount
        {
            get => _shiftCount;
            set
            {
                _shiftCount = value;
                MuteChannel = shouldMuteChannel();
            }
        }
        public ushort ChannelPeriod { get; set; }

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

            this._divider.Clock();

            if (this.Reload)
            {
                this._divider.Reset();
                this.Reload = false;
            }
        }

        private void divider_ReachedZero(object sender, EventArgs e)
        {
            if (!this.MuteChannel && Enabled && _shiftCount > 0)
            {
                this.ChannelPeriod = (ushort)this._targetPeriod;
                this.MuteChannel = shouldMuteChannel();
                //this.PeriodUpdate?.Invoke(this, EventArgs.Empty);
                _periodUpdate(this, EventArgs.Empty);
            }
            else
                Log.Debug("Sweep unit is muting channel");
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
            this.MuteChannel = shouldMuteChannel();
        }

        private bool shouldMuteChannel() => _targetPeriod > MAX_TARGET_PERIOD || ChannelPeriod < 8;
    }
}
