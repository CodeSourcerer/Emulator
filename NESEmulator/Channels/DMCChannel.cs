using System;
using System.Collections.Generic;
using log4net;
using NESEmulator.APU;
using NESEmulator.Util;

namespace NESEmulator.Channels
{
    public class DMCChannel : Channel
    {
        private const ushort ADDR_FLAGSANDRATE  = 0x4010;
        private const ushort ADDR_DIRECTLOAD    = 0x4011;
        private const ushort ADDR_SAMPLEADDR    = 0x4012;
        private const ushort ADDR_SAMPLELENGTH  = 0x4013;

        private static readonly ILog Log = LogManager.GetLogger(typeof(DMCChannel));
        private static readonly ushort[] _rateTableNTSC = 
            { 428, 380, 340, 320, 286, 254, 226, 214, 190, 160, 142, 128, 106, 84, 72, 54 };

        public short Output { get; private set; }
        public bool Enabled
        {
            get => _bytesRemaining > 0;
            set
            {
                if (!value)
                    _bytesRemaining = 0;
                else
                {
                    if (_bytesRemaining == 0)
                        restartSample();
                    else
                        Log.Debug($"DMC Enabled before sample finished. DMC Sample will restart after {_bytesRemaining} bytes are processed.");
                }
            }
        }

        public byte FlagsAndRate { get; set; }

        public bool IRQEnable { get; set; }

        public bool Loop { get; set; }

        public byte RateIndex { get; set; }

//        public byte DirectLoad { get; set; }

        public ushort SampleAddress { get; set; }

        public ushort SampleLength { get; set; }

        private APUDivider _dmcTimer;
        private ushort _samplePtr;
        private ushort _bytesRemaining;
        private CS2A03 _apu;
        private byte _sampleBuffer;
        private byte _shifter;
        private byte _bitsRemaining;
        private bool _silence;  // I keel you!
        private bool _interruptOccurred;
        public bool InterruptFlag { get; set; }

        public DMCChannel(CS2A03 apu)
        {
            _apu = apu;
            _dmcTimer = new APUDivider(timer_Elapsed);
            _sampleBuffer = 0;
            Output = 0;
            Loop = false;
        }

        private void timer_Elapsed(object sender, EventArgs e)
        {
            if (_bitsRemaining == 0)
            {
                // New output cycle
                _bitsRemaining = 8;

                getNextSample();

                if (_bytesRemaining == 0)
                {
                    _silence = true;
                    Output = 0;
                }
                else
                {
                    _silence = false;
                    _shifter = _sampleBuffer;
                    _sampleBuffer = 0;
                }
            }

            if (!_silence)
            {
                bool add = (_shifter & 0x01) == 1;

                _shifter >>= 1;
                if (add && Output < 126)
                    Output += 2;
                else if (!add && Output > 2)
                    Output -= 2;
            }

            --_bitsRemaining;
        }

        public void ClockHalfFrame(ulong cpuCycle)
        {
            
        }

        public void ClockQuarterFrame(ulong cpuCycle)
        {
            
        }

        /// <summary>
        /// Clock the channel (as if from timer)
        /// </summary>
        /// <param name="clockCycles"></param>
        public void Clock(ulong clockCycles)
        {
            MemoryReader memReader = _apu.GetMemoryReader();
            if (memReader.BufferReady)
                _sampleBuffer = memReader.Buffer;

            if (InterruptFlag == true)
            {
                if (!_interruptOccurred)
                {
                    _apu.IRQ();
                    _interruptOccurred = true;
                    Log.Debug("DMC IRQ");
                }
            }
            else
            {
                _interruptOccurred = false;
            }

            if (clockCycles % 3 == 0)
            {
                _dmcTimer.Clock();
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
                    _dmcTimer.CounterReload = _rateTableNTSC[RateIndex];
                    if (!IRQEnable)
                    {
                        InterruptFlag = false;
                        _apu.ClearIRQ();
                        Log.Debug("DMC IRQ Disabled");
                    }
                    Log.Debug($"DMC Channel written: [IRQEnable={IRQEnable}] [Loop={Loop}] [RateIndex={RateIndex:X2}]");
                    break;

                case ADDR_DIRECTLOAD:
                    Output = (byte)(data & 0x7F);
                    Log.Debug($"DMC Channel written: [DirectLoad={Output}]");
                    break;

                case ADDR_SAMPLEADDR:
                    SampleAddress = (ushort)(0xC000 | (data << 6));
                    Log.Debug($"DMC Channel written: [SampleAddress={SampleAddress:X4}]");
                    break;

                case ADDR_SAMPLELENGTH:
                    SampleLength = (ushort)((data << 4) + 1);
                    //if (_bytesRemaining == 0)
                    //    _bytesRemaining = SampleLength;
                    Log.Debug($"DMC Chennel written: [SampleLength={SampleLength}]");
                    break;
            }
        }

        private void getNextSample()
        {
            if (_bytesRemaining > 0)
            {
                if (SampleLength == 1)
                {
                    Log.Debug("DMC Using previous sample loaded");
                    _shifter = _sampleBuffer = _apu.GetMemoryReader().Buffer; // use last value
                    _bitsRemaining = 8;
                    _silence = false;
                }
                else
                {
                    Log.Debug("DMC Fetching sample byte");
                    _apu.InitiateDMCSampleFetch(4, _samplePtr);
                }
                if (++_samplePtr == 0x0000)
                {
                    _samplePtr = 0x8000;
                    Log.Debug("DMC Sample ptr wrapped!");
                }
                if (--_bytesRemaining == 0)
                {
                    Log.Debug("End of DMC Sample");
                    if (Loop)
                        restartSample();
                    else if (IRQEnable)
                    {
                        InterruptFlag = true;
                        Log.Debug("End of Sample: DMC InterruptFlag set");
                    }
                }
            }
            else
            {
                _sampleBuffer = 0;
            }
        }

        private void restartSample()
        {
            Log.Debug("Restarting DMC sample");
            _samplePtr = SampleAddress;
            _bytesRemaining = SampleLength;
        }
    }
}
