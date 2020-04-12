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
        public byte ShiftCount { get; set; }

        // This represents the period of the sweep
        private APUDivider _divider;
        private APUSequencer _pulseSequencer;
        private int _pulseChannelNumber;
        private int _targetPeriod;

        public APUSweep(int pulseChannelNumber, APUSequencer pulseSequencer)
        {
            _pulseChannelNumber = pulseChannelNumber;
            _pulseSequencer = pulseSequencer;
            _divider = new APUDivider(divider_ReachedZero);
        }

        public void Clock()
        {
            calcTargetPeriod();

            _divider.Clock();

            if (Reload)
            {
                Reload = false;
                _divider.Reset();
            }
        }

        private void divider_ReachedZero(object sender, EventArgs e)
        {
            if (!MuteChannel && Enabled && ShiftCount != 0)
            {
                // Update current period
                _pulseSequencer.TimerReload = (ushort)_targetPeriod;
                if (_pulseSequencer.TimerReload < 8)
                {
                    if (!MuteChannel)
                        Log.Debug($"Sweep muted channel. [_targetPeriod={_targetPeriod:X2}] [ShiftCount={ShiftCount}] [TimerReload={_pulseSequencer.TimerReload}]");

                    MuteChannel = true;
                }
            }
            _divider.Reset();
            Reload = false;
        }

        private void calcTargetPeriod()
        {
            int shiftAmount = _pulseSequencer.TimerReload >> ShiftCount;

            if (Negate)
            {
                if (_pulseChannelNumber == 1)
                    shiftAmount = ~shiftAmount; // take 1's compliment
                else
                    shiftAmount = -shiftAmount; // take 2's compliment
            }

            _targetPeriod = _pulseSequencer.TimerReload + shiftAmount;
            if (_targetPeriod > MAX_TARGET_PERIOD)
            {
                if (!MuteChannel)
                    Log.Debug($"Sweep muted channel. [_targetPeriod={_targetPeriod:X2}] [ShiftCount={ShiftCount}] [TimerReload={_pulseSequencer.TimerReload}]");

                MuteChannel = true;
            }
        }
    }
}
