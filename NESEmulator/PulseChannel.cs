using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public class PulseChannel : Channel
    {
        public int ChannelNum { get; private set; }

        private APULengthCounter _lengthCounter;

        private const byte DUTY_CYCLE_0 = 0b01000000;
        private const byte DUTY_CYCLE_1 = 0b01100000;
        private const byte DUTY_CYCLE_2 = 0b01111000;
        private const byte DUTY_CYCLE_3 = 0b10011111;

        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
            this._lengthCounter = new APULengthCounter();
            this._lengthCounter.CounterElapsed += generateWave;
        }

        private void generateWave(object sender, EventArgs e)
        {
            // Set output
        }

        public void Clock(ulong clockCycles)
        {
            if (clockCycles % 6 == 0)
            {
                _lengthCounter.Clock();
            }
        }

        public void ClockHalfFrame()
        {
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
                Console.WriteLine($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty: {data >> 6}] [Halt: {_lengthCounter.Halt}] [ConstantVolume: {data.TestBit(4)}] [Volume: {data & 0x0F}]");
            }
            if (addr == 0x4001 || addr == 0x4005)
            {
                _lengthCounter.Enabled = data.TestBit(7);
                Console.WriteLine($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Enabled: {_lengthCounter.Enabled}]");
            }
            // Pulse channel 1 & 2 timer low bits
            if (addr == 0x4002 || addr == 0x4006)
            {
                _lengthCounter.TimerReload &= 0xFF00;
                _lengthCounter.TimerReload |= data;
                Console.WriteLine($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [TimerReload: {_lengthCounter.TimerReload}]");
            }
            // Pulse channel 1 & 2 length counter load and timer high bits
            if (addr == 0x4003 || addr == 0x4007)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _lengthCounter.TimerReload &= 0x00FF;
                _lengthCounter.TimerReload |= (ushort)((data & 0x07) << 8);
                Console.WriteLine($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength: {_lengthCounter.LinearLength}] [TimerReload: {_lengthCounter.TimerReload}]");
            }
        }
    }
}
