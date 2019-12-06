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

        private APULengthCounter _lengthCounter;
        private APUSequencer _sequencer;

        private static byte[] DUTY_CYCLE = new byte[]
        {
            0b00000001,
            0b00000011,
            0b00001111,
            0b11111100
        };

        private byte _dutyCycle;
        private int _dutyCycleIndex;
        private byte _volume;
        private List<short> _buffer;
        private short _output;
        public short Output
        {
            get { return _output; }
        }

        public bool Enabled
        {
            set
            {
                this._lengthCounter.ClearLength();
            }
        }


        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
            this._dutyCycleIndex = 0;
            this._dutyCycle = DUTY_CYCLE[0];
            this._buffer = new List<short>(50);
            this._lengthCounter = new APULengthCounter();
            this._lengthCounter.CounterElapsed += lengthCounterElapsed;
            this._lengthCounter.Enabled = true;
            this._sequencer = new APUSequencer();
            this._sequencer.TimerElapsed += generateWave;
        }

        private void lengthCounterElapsed(object sender, EventArgs e)
        {

        }

        private void generateWave(object sender, EventArgs e)
        {
            short volume = (short)(short.MaxValue * 0.25);
            _output = _dutyCycle.TestBit(_dutyCycleIndex) ? volume : (short)(-volume);
            //if (_lengthCounter.TimerReload > 8 && _lengthCounter.LinearLength > 0 && this._volume > 0)
            {
                _dutyCycleIndex += 1;
                _dutyCycleIndex %= 8;
            }
        }

        public void Clock(ulong clockCycles)
        {
            if (clockCycles % 6 == 0)
            {
                _sequencer.Clock();
                _buffer.Add(_output);
            }
        }

        public void ClockHalfFrame()
        {
            _lengthCounter.Clock();
        }

        public void ClockQuarterFrame()
        {
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
                this._volume = (byte)(data & 0x0F);
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty={data >> 6}] [Halt={_lengthCounter.Halt}] [ConstantVolume={data.TestBit(4)}] [Volume={this._volume}]");
            }
            if (addr == 0x4001 || addr == 0x4005)
            {
                // This should be sweep enabled
                //_lengthCounter.Enabled = data.TestBit(7);
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written.");
            }
            // Pulse channel 1 & 2 timer low bits
            if (addr == 0x4002 || addr == 0x4006)
            {
                _sequencer.TimerReload &= 0xFF00;
                _sequencer.TimerReload |= data;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [TimerReload={_sequencer.TimerReload}]");
            }
            // Pulse channel 1 & 2 length counter load and timer high bits
            if (addr == 0x4003 || addr == 0x4007)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _sequencer.TimerReload &= 0x00FF;
                _sequencer.TimerReload |= (ushort)((data & 0x07) << 8);
                _dutyCycleIndex = 0;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength={_lengthCounter.LinearLength}] [TimerReload={_sequencer.TimerReload}]");
            }
        }

        public short GetOutput()
        {
            short[] buffer = this._buffer.ToArray();
            this._buffer.Clear();
            //long average = 0;
            //foreach (var bufferVal in buffer)
            //    average += bufferVal;
            //average /= buffer.Length;

            return (short)_output;
        }

    }
}
