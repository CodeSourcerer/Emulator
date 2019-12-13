using System;
using System.Collections.Generic;
using NESEmulator.Util;

namespace NESEmulator.Channels
{
    public class DMCChannel : Channel
    {
        private const ushort ADDR_FLAGSANDRATE  = 0x4010;
        private const ushort ADDR_DIRECTLOAD    = 0x4011;
        private const ushort ADDR_SAMPLEADDR    = 0x4012;
        private const ushort ADDR_SAMPLELENGTH  = 0x4013;

        private static readonly ushort[] _rateTableNTSC = 
            { 428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54 };

        public short Output { get; private set; }
        public bool Enabled { get; set; }

        public byte FlagsAndRate { get; set; }

        public bool IRQEnable { get; set; }

        public bool Loop { get; set; }

        public byte RateIndex { get; set; }

        /// <summary>
        /// Output level
        /// </summary>
        public byte DirectLoad { get; set; }

        public ushort SampleAddress { get; set; }

        public ushort SampleLength { get; set; }

        private ushort _addressPtr;
        private CS2A03 _apu;
        private byte _rightShiftReg;
        private byte _bitsRemaining;
        private bool _silence;  // I keel you!

        public DMCChannel(CS2A03 apu)
        {
            _apu = apu;
            Output = 0;
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
                    IRQEnable    = data.TestBit(7);
                    Loop         = data.TestBit(6);
                    RateIndex    = (byte)(data & 0x0F);
                    FlagsAndRate = data;
                    //Console.WriteLine("DMC Flags and Rate: {0:X2}", data);
                    break;

                case ADDR_DIRECTLOAD:
                    DirectLoad = (byte)(data & 0x7F);
                    //Console.WriteLine("DMC Direct Load: {0:X2}", data);
                    break;

                case ADDR_SAMPLEADDR:
                    SampleAddress = (ushort)(0xC000 | (data << 6));
                    //Console.WriteLine("DMC Sample Address set: {0:X2}", data);
                    break;

                case ADDR_SAMPLELENGTH:
                    SampleLength = (ushort)((data << 4) + 1);
                    //Console.WriteLine("DMC Sample Length set: {0:X2}", data);
                    break;
            }
        }
    }
}
