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
        public bool MuteChannel { get; set; }
        public int DividerPeriod
        {
            get => _divider.CounterReload;
            set => _divider.CounterReload = value;
        }
<<<<<<< HEAD
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
=======
        public byte ShiftCount { get; set; }
>>>>>>> ce03e04b96b58e1b8417a649d2d96dd97627ed52
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
            _divider = new APUDivider(divider_ReachedZero);
            //this._divider.DividerReachedZero += divider_ReachedZero;
        }

        public void Clock()
        {
            calcTargetPeriod();

<<<<<<< HEAD
            this._divider.Clock();

            if (this.Reload)
            {
                this._divider.Reset();
                this.Reload = false;
            }
=======
            _divider.Clock();
>>>>>>> ce03e04b96b58e1b8417a649d2d96dd97627ed52
        }

        private void divider_ReachedZero(object sender, EventArgs e)
        {
<<<<<<< HEAD
            if (!this.MuteChannel && Enabled && _shiftCount > 0)
            {
                this.ChannelPeriod = (ushort)this._targetPeriod;
                this.MuteChannel = shouldMuteChannel();
                //this.PeriodUpdate?.Invoke(this, EventArgs.Empty);
                _periodUpdate(this, EventArgs.Empty);
            }
            else
                Log.Debug("Sweep unit is muting channel");
=======
            if (!MuteChannel && Enabled)
            {
                ChannelPeriod = (ushort)_targetPeriod;
                _periodUpdate(this, EventArgs.Empty);
            }

            if (Reload)
            {
                Reload = false;
                _divider.Reset();
            }
>>>>>>> ce03e04b96b58e1b8417a649d2d96dd97627ed52
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
<<<<<<< HEAD

        private bool shouldMuteChannel() => _targetPeriod > MAX_TARGET_PERIOD || ChannelPeriod < 8;
=======
>>>>>>> ce03e04b96b58e1b8417a649d2d96dd97627ed52
    }
}
