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

        private static byte[] DUTY_CYCLE = new byte[]
        {
            0b00000001,
            0b00000011,
            0b00001111,
            0b11111100
        };

        private byte _dutyCycle;
        private int _dutyCycleIndex;
        private bool _constantVolume;
        private byte _volume;
        private int _bufferWritePtr;
        private short[] _buffer;
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

        public const int CHANNEL_BUFFER_SIZE = (int)(CLOCK_NTSC_APU * (CS2A03.SOUND_BUFFER_SIZE_MS / 1000.0)) + 1;

        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
            this._dutyCycleIndex = 0;
            this._dutyCycle = DUTY_CYCLE[0];
            this._buffer = new short[CHANNEL_BUFFER_SIZE];
            this._lengthCounter = new APULengthCounter();
            this._lengthCounter.CounterElapsed += lengthCounterElapsed;
            this._lengthCounter.Enabled = true;
            this._sequencer = new APUSequencer(generateWave);
        }

        private void lengthCounterElapsed(object sender, EventArgs e)
        {

        }

        private void generateWave()
        {
            short volume = (short)(short.MaxValue >> 4);
            _output = _dutyCycle.TestBit(_dutyCycleIndex) ? volume : (short)(-volume);
            if (_lengthCounter.LinearLength > 0 && _volume > 0)
            {
                _dutyCycleIndex = (++_dutyCycleIndex) & 7;
            }
        }

        public bool IsBufferFull() => !(_bufferWritePtr < CHANNEL_BUFFER_SIZE);

        public void Clock(ulong clockCycles)
        {
            if (clockCycles % 6 == 0)
            {
                _sequencer.Clock();
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
                this._constantVolume = data.TestBit(4);
                this._volume = (byte)(data & 0x0F);
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty={data >> 6}] [Halt={_lengthCounter.Halt}] [ConstantVolume={data.TestBit(4)}] [Volume={this._volume}]");
            }
            else if (addr == 0x4001 || addr == 0x4005)
            {
                // This should be sweep enabled
                //_lengthCounter.Enabled = data.TestBit(7);
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written.");
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
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength={_lengthCounter.LinearLength}] [TimerReload={_sequencer.TimerReload}]");
            }
        }

        //public short GetOutput()
        //{
        //    short[] buffer = this._buffer.ToArray();
        //    this._buffer.Clear();
        //    long average = 0;
        //    foreach (var bufferVal in buffer)
        //        average += bufferVal;
        //    average /= buffer.Length;

        //    return (short)average;
        //}

        public short[] ReadAndResetBuffer()
        {
            _bufferWritePtr = 0;
            return _buffer;
        }

        public short[] GenerateWave(int lengthInMS)
        {
            int freq = (int)(CLOCK_NTSC_APU / (16.0 * _sequencer.TimerReload + 1));
            int dataCount = (int)(44100 * (lengthInMS / 1000.0f));
            double dt = 2 * Math.PI / 44100;
            var data = new short[dataCount];
            short amp = short.MaxValue >> 2;

            //Log.Debug($"Generating pulse wave for {lengthInMS} ms, freq {freq} hz");

            var cacheDTxFreq = dt * freq;
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = (short)(amp * Math.Sign(Math.Sin(i * cacheDTxFreq)));
            }

            return data;
        }
    }
}
