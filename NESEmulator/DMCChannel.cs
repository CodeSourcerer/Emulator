using System;
using System.Collections.Generic;
using NESEmulator.Util;

namespace NESEmulator
{
    public class DMCChannel : Channel
    {
        private const ushort ADDR_FLAGSANDRATE  = 0x4010;
        private const ushort ADDR_DIRECTLOAD    = 0x4011;
        private const ushort ADDR_SAMPLEADDR    = 0x4012;
        private const ushort ADDR_SAMPLELENGTH  = 0x4013;

        public byte FlagsAndRate { get; set; }

        public bool IRQEnable
        {
            get
            {
                return FlagsAndRate.TestBit(7);
            }
            set
            {
                FlagsAndRate.SetBit(7, value);
            }
        }

        public bool Loop
        {
            get
            {
                return FlagsAndRate.TestBit(6);
            }
            set
            {
                FlagsAndRate.SetBit(6, value);
            }
        }

        private Dictionary<byte, ushort> _rateTableNTSC;
        public byte RateIndex
        {
            get
            {
                return (byte)(FlagsAndRate & 0x0F);
            }
            set
            {
                FlagsAndRate = (byte)((FlagsAndRate & 0xF0) | value);
            }
        }

        /// <summary>
        /// Output level
        /// </summary>
        public byte DirectLoad { get; set; }

        private byte _sampleAddress;
        public ushort SampleAddress
        {
            get
            {
                return (ushort)(0xC000 | (_sampleAddress << 6));
            }
            set
            {
                _sampleAddress = (byte)value;
            }
        }

        private byte _sampleLength;
        public ushort SampleLength
        {
            get
            {
                return (ushort)((_sampleLength << 4) + 1);
            }
            set
            {
                _sampleLength = (byte)value;
            }
        }

        private CS2A03 _apu;
        private byte _rightShiftReg;
        private byte _bitsRemaining;
        private bool _silence;  // I keel you!

        public DMCChannel(CS2A03 apu)
        {
            this._apu = apu;
            generateRateTableNTSC();
        }

        public void ClockHalfFrame()
        {
            
        }

        public void ClockQuarterFrame()
        {
            
        }

        /// <summary>
        /// Clock the channel (as if from timer)
        /// </summary>
        /// <param name="clockCycles"></param>
        public void Clock(ulong clockCycles)
        {
            if (_bitsRemaining == 0)
            {
                _bitsRemaining = 8;
            }
        }

        public byte Read(ushort addr)
        {
            return 0;
        }

        public void Write(ushort addr, byte data)
        {
            // Figure out what we're writing to
            switch (addr)
            {
                case ADDR_FLAGSANDRATE:
                    this.FlagsAndRate = data;
                    //Console.WriteLine("DMC Flags and Rate: {0:X2}", data);
                    break;

                case ADDR_DIRECTLOAD:
                    this.DirectLoad = data;
                    //Console.WriteLine("DMC Direct Load: {0:X2}", data);
                    break;

                case ADDR_SAMPLEADDR:
                    this.SampleAddress = data;
                    //Console.WriteLine("DMC Sample Address set: {0:X2}", data);
                    break;

                case ADDR_SAMPLELENGTH:
                    this.SampleLength = data;
                    //Console.WriteLine("DMC Sample Length set: {0:X2}", data);
                    break;
            }
        }

        private void generateRateTableNTSC()
        {
            _rateTableNTSC = new Dictionary<byte, ushort>(16)
            {
                { 0x00, 428 },
                { 0x01, 380 },
                { 0x02, 340 },
                { 0x03, 320 },
                { 0x04, 286 },
                { 0x05, 254 },
                { 0x06, 226 },
                { 0x07, 214 },
                { 0x08, 190 },
                { 0x09, 160 },
                { 0x0A, 142 },
                { 0x0B, 128 },
                { 0x0C, 106 },
                { 0x0D, 84  },
                { 0x0E, 72  },
                { 0x0F, 54  }
            };
        }
    }
}
