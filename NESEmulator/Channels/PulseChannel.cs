using System;
using System.Collections.Generic;
using log4net;
using NESEmulator.APU;
using NESEmulator.Util;

namespace NESEmulator.Channels
{
    public class PulseChannel : Channel
    {
        public int ChannelNum { get; private set; }

        private static ILog Log = LogManager.GetLogger(typeof(PulseChannel));

        private APULengthCounter _lengthCounter;
        private APUDivider _sequencer;
        private APUVolumeEnvelope _volumeEnvelope;
        private APUSweep _sweepUnit;

        private static byte[] DUTY_CYCLE = 
        {
            0b00000001,
            0b00000011,
            0b00001111,
            0b11111100
        };

        private byte _dutyCycle;
        private int _dutyCycleIndex;
        public short Output { get; private set; }

        public bool Enabled
        {
            set
            {
                if (!value)
                {
                    _lengthCounter.ClearLength();
                    Output = 0;
                }
            }
            get
            {
                return _lengthCounter.Length > 0;
            }
        }

        public PulseChannel(int channel)
        {
            ChannelNum = channel;
            _volumeEnvelope = new APUVolumeEnvelope();
            _dutyCycleIndex = 0;
            _dutyCycle = DUTY_CYCLE[0];
            _lengthCounter = new APULengthCounter(lengthCounterElapsed);
            _lengthCounter.Enabled = true;
            //_sequencer.OnTimerElapsed += sequencer_GenerateWave;
            _sequencer = new APUDivider(sequencer_GenerateWave);
            _sweepUnit = new APUSweep(channel, _sequencer);
        }

        private void lengthCounterElapsed(object sender, EventArgs e)
        {

        }

        /// <summary>
        /// When the sequencer ticks down to 0, this will advance the duty cycle index generating
        /// the appropriate square wave form.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void sequencer_GenerateWave(object sender, EventArgs e)
        {
            _dutyCycleIndex = (--_dutyCycleIndex) & 7;
            if (!isChannelMuted())
            {
                Output = _dutyCycle.TestBit(_dutyCycleIndex) ? _volumeEnvelope.Volume : (short)0;
            }
            else
            {
                Output = 0;
            }
        }

        public void Clock(ulong clockCycles)
        {
            if (clockCycles % 6 == 0)
            {
                // Clock the sequencer every APU cycle
                _sequencer.Clock();
            }
        }

        /// <summary>
        /// Called from APU when FrameCounter detects we are on a half frame.
        /// </summary>
        /// <remarks>
        /// This is where we update the length counter and sweep unit.
        /// </remarks>
        public void ClockHalfFrame()
        {
            _sweepUnit.Clock();
            _lengthCounter.Clock();
        }

        /// <summary>
        /// Called from APU when FrameCounter detects we are on a quarter frame.
        /// </summary>
        /// <remarks>
        /// This is where we update envelopes
        /// </remarks>
        public void ClockQuarterFrame()
        {
            _volumeEnvelope.Clock();
        }

        public byte Read(ushort addr)
        {
            throw new NotImplementedException();
        }

        public void Write(ushort addr, byte data)
        {
            if (addr == 0x4000 || addr == 0x4004)
            {
                _lengthCounter.Halt = data.TestBit(5);
                _dutyCycle = DUTY_CYCLE[data >> 6];
                _volumeEnvelope.ConstantVolume = data.TestBit(4);
                _volumeEnvelope.Volume = (byte)(data & 0x0F);
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty={data >> 6}] [Halt={_lengthCounter.Halt}] [ConstantVolume={_volumeEnvelope.ConstantVolume}] [Volume/Period={_volumeEnvelope.Volume}]");
            }
            else if (addr == 0x4001 || addr == 0x4005)
            {
                _sweepUnit.Enabled       = data.TestBit(7);
                _sweepUnit.Negate        = data.TestBit(3);
                _sweepUnit.DividerPeriod = ((data >> 4) & 7) + 1;
                _sweepUnit.ShiftCount    = (byte)(data & 7);
                _sweepUnit.Reload        = true;
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [SweepEnabled={_sweepUnit.Enabled}] [DividerPeriod={_sweepUnit.DividerPeriod}] [Negate={_sweepUnit.Negate}] [ShiftCount={_sweepUnit.ShiftCount}]");
            }
            // Pulse channel 1 & 2 timer low bits
            else if (addr == 0x4002 || addr == 0x4006)
            {
                _sequencer.CounterReload &= 0xFF00; // Preserve data in high byte, clearing data in low byte
                _sequencer.CounterReload |= data;
            }
            // Pulse channel 1 & 2 length counter load and timer high bits
            else if (addr == 0x4003 || addr == 0x4007)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _sequencer.CounterReload &= 0x00FF; // Clear data in high byte, preserving data in low byte
                _sequencer.CounterReload |= (ushort)((data & 0x07) << 8) + 1;

                _dutyCycleIndex = 0;
                _volumeEnvelope.Start = true;
                _sweepUnit.MuteChannel = false;
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength={_lengthCounter.Length}] [TimerReload={_sequencer.TimerReload}]");
            }
        }

        private bool isChannelMuted()
        {
            bool isMuted = (_lengthCounter.Length == 0 ||
                            _sequencer.CounterReload < 8 ||
                            _sweepUnit.MuteChannel);

            return isMuted;
        }
    }
}
