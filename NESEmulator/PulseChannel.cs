using System;
using System.Collections.Generic;
using log4net;
using NESEmulator.Util;

namespace NESEmulator
{
    public class PulseChannel : Channel
    {
        public int ChannelNum { get; private set; }

        private static ILog Log = LogManager.GetLogger(typeof(PulseChannel));
        private const double CLOCK_NTSC_APU = 894886.5;

        private APULengthCounter _lengthCounter;
        private APUSequencer _sequencer;
        private APUVolumeEnvelope _volumeEnvelope;
        private APUSweep _sweepUnit;

        private static byte[] DUTY_CYCLE = new byte[]
        {
            0b00000001,
            0b00000011,
            0b00001111,
            0b11111100
        };

        private static readonly short[] VOLUME_LOOKUP = new short[] {     0,  2184,  4369,  6553,  8738, 10922, 13107, 15291, 
                                                                      17476, 19660, 21845, 24029, 26214, 28398, 30583, 32767 };
        private byte _dutyCycle;
        private int _dutyCycleIndex;
        private int _bufferWritePtr;
        private short[] _buffer;
        public short Output { get; private set; }

        public bool Enabled
        {
            set
            {
                if (!value)
                    this._lengthCounter.ClearLength();
            }
            get
            {
                return this._lengthCounter.LinearLength > 0;
            }
        }

        public const int CHANNEL_BUFFER_SIZE = (int)(CLOCK_NTSC_APU * (CS2A03.SOUND_BUFFER_SIZE_MS / 1000.0)) + 1;

        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
            this._volumeEnvelope = new APUVolumeEnvelope();
            this._sweepUnit = new APUSweep(channel);
            this._sweepUnit.PeriodUpdate += _sweepUnit_PeriodUpdate;
            this._dutyCycleIndex = 0;
            this._dutyCycle = DUTY_CYCLE[0];
            this._buffer = new short[CHANNEL_BUFFER_SIZE];
            this._lengthCounter = new APULengthCounter();
            this._lengthCounter.CounterElapsed += lengthCounterElapsed;
            this._lengthCounter.Enabled = true;
            this._sequencer = new APUSequencer();
            this._sequencer.OnTimerElapsed += sequencer_GenerateWave;
        }

        private void _sweepUnit_PeriodUpdate(object sender, EventArgs e)
        {
            this._sequencer.TimerReload = this._sweepUnit.ChannelPeriod;
        }

        private void lengthCounterElapsed(object sender, EventArgs e)
        {

        }

        private void sequencer_GenerateWave(object sender, EventArgs e)
        {
            this.Output = _dutyCycle.TestBit(_dutyCycleIndex) ? VOLUME_LOOKUP[_volumeEnvelope.Volume] : (short)(-VOLUME_LOOKUP[_volumeEnvelope.Volume]);
            if (!isChannelMuted())
            {
                _dutyCycleIndex = (++_dutyCycleIndex) & 7;
            }
        }

        public bool IsBufferFull() => !(_bufferWritePtr < CHANNEL_BUFFER_SIZE);

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
            _lengthCounter.Clock();
            _sweepUnit.Clock();
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
                this._lengthCounter.Halt = data.TestBit(5);
                this._dutyCycle = DUTY_CYCLE[data >> 6];
                this._volumeEnvelope.ConstantVolume = data.TestBit(4);
                this._volumeEnvelope.Volume = (byte)(data & 0x0F);
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty={data >> 6}] [Halt={_lengthCounter.Halt}] [ConstantVolume={_volumeEnvelope.ConstantVolume}] [Volume={_volumeEnvelope.Volume}]");
            }
            else if (addr == 0x4001 || addr == 0x4005)
            {
                // This should be sweep enabled
                this._sweepUnit.Enabled = data.TestBit(7);
                this._sweepUnit.Negate = data.TestBit(3);
                this._sweepUnit.DividerPeriod = ((data >> 4) & 7) + 1;
                this._sweepUnit.ShiftCount = (byte)(data & 7);
                this._sweepUnit.Reload = true;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [SweepEnabled={_sweepUnit.Enabled}] [DividerPeriod={_sweepUnit.DividerPeriod}] [Negate={_sweepUnit.Negate}] [ShiftCount={_sweepUnit.ShiftCount}]");
            }
            // Pulse channel 1 & 2 timer low bits
            else if (addr == 0x4002 || addr == 0x4006)
            {
                _sequencer.TimerReload &= 0xFF00;
                _sequencer.TimerReload |= data;
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [TimerReload={_sequencer.TimerReload}]");
            }
            // Pulse channel 1 & 2 length counter load and timer high bits
            else if (addr == 0x4003 || addr == 0x4007)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _sequencer.TimerReload &= 0x00FF;
                _sequencer.TimerReload |= (ushort)((data & 0x07) << 8);
                _dutyCycleIndex = 0;
                _volumeEnvelope.Start = true;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength={_lengthCounter.LinearLength}] [TimerReload={_sequencer.TimerReload}]");
            }
        }

        public short[] ReadAndResetBuffer()
        {
            _bufferWritePtr = 0;
            return _buffer;
        }

        private bool isChannelMuted()
        {
            bool isMuted = (_lengthCounter.LinearLength == 0 ||
                            _volumeEnvelope.Volume == 0 ||
                            Output == 0 ||
                            _sequencer.TimerReload < 8 &&
                            _sweepUnit.MuteChannel);

            return isMuted;
        }
    }
}
