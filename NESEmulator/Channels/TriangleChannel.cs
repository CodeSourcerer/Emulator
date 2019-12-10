using System;
using log4net;
using NESEmulator.APU;
using NESEmulator.Util;

namespace NESEmulator.Channels
{
    public class TriangleChannel : Channel
    {
        private const ushort ADDR_LINEARCOUNTER          = 0x4008;
        private const ushort ADDR_TIMERLOW               = 0x400A;
        private const ushort ADDR_TIMERHI_LENCOUNTERLOAD = 0x400B;

        private static ILog Log = LogManager.GetLogger(typeof(TriangleChannel));

        private static readonly short[] VOLUME_LOOKUP = new short[] { -32768,  -28399,  -24030,  -19661,  -15292, -10923, -6554, -2185,
                                                                        2184,    6553,   10922,   15921,   19660,  24029, 28398, 32767 };

        //private APULengthCounter _lengthCounter;
        private APUSequencer _sequencer;
        private bool _linearControlFlag;
        private bool _linearCounterReloadFlag;
        private byte _linearCounterReload;
        private byte _linearCounter;
        private int _sequenceDirection;
        private int _sequencePtr;

        public short Output { get; private set; }
        public bool Enabled
        {
            set
            {
                if (!value)
                    _linearCounterReload = 0;
                    //_lengthCounter.ClearLength();
            }
            get
            {
                return true;
                //return _lengthCounter.Length > 0;
            }
        }

        public bool ChannelMuted { get; private set; }

        public TriangleChannel()
        {
            //_lengthCounter = new APULengthCounter();
            //_lengthCounter.Enabled = true;
            _sequencer = new APUSequencer();
            _sequencer.OnTimerElapsed += sequencer_GenerateWave;

            _sequenceDirection = -1;
            _sequencePtr = 15;
        }

        private void sequencer_GenerateWave(object sender, EventArgs e)
        {
            this.Output = VOLUME_LOOKUP[_sequencePtr];

            if (!ChannelMuted)//  && _linearCounter > 0)
            {
                if (_sequenceDirection < 0 && _sequencePtr == 0)
                {
                    _sequenceDirection = 1;
                }
                else if (_sequenceDirection > 0 && _sequencePtr == 15)
                {
                    _sequenceDirection = -1;
                }
                else
                {
                    _sequencePtr += _sequenceDirection;
                }
            }
        }

        /// <summary>
        /// Called from APU when FrameCounter detects we are on a half frame.
        /// </summary>
        /// <remarks>
        /// This is where we update the length counter.
        /// </remarks>
        public void ClockHalfFrame()
        {
            //_lengthCounter.Clock();
        }

        /// <summary>
        /// Called from APU when FrameCounter detects we are on a quarter frame.
        /// </summary>
        /// <remarks>
        /// This is where we update envelopes and the linear counter
        /// </remarks>
        public void ClockQuarterFrame()
        {
            if (_linearCounter == 0 && _linearCounterReloadFlag) // && _linearCounter == 0)
            {
                _linearCounter = _linearCounterReload;
            }
            else if (_linearCounter > 0)
            {
                --_linearCounter;
            }
            ChannelMuted = _linearCounter == 0;

            if (!_linearControlFlag) //_lengthCounter.Halt) // also control flag
                _linearCounterReloadFlag = false;
        }

        public void Clock(ulong clockCyles)
        {
            if (clockCyles % 3 == 0)
            {

                // Sequencer is clocked at CPU rate on the triangle channel
                if (_linearCounter > 0) // && _lengthCounter.Length > 0)
                    _sequencer.Clock();
            }
        }

        public byte Read(ushort addr)
        {
            return 0;
        }

        public void Write(ushort addr, byte data)
        {
            switch (addr)
            {
                case ADDR_LINEARCOUNTER:
                    //_lengthCounter.Halt = data.TestBit(7);
                    _linearControlFlag = data.TestBit(7);
                    _linearCounterReload = (byte)(data & 0x7F);
                    _linearCounter = _linearCounterReload;
                    Log.Debug($"Triangle channel write: [LinearControlFlag={_linearControlFlag}] [LinearLength={_linearCounterReload}]");
                    break;
                case ADDR_TIMERLOW:
                    _sequencer.TimerReload &= 0xFF00; // Preserve data in high byte, clearing data in low byte
                    _sequencer.TimerReload |= data;
                    break;
                case ADDR_TIMERHI_LENCOUNTERLOAD:
                    //_lengthCounter.LoadLength((byte)(data >> 3));
                    _sequencer.TimerReload &= 0x00FF; // Clear data in high byte, preserving data in low byte
                    _sequencer.TimerReload |= (ushort)((data & 0x07) << 8);
                    _linearCounterReloadFlag = true;
                    Log.Debug($"Triangle channel write: [data={data:X2}] [Timer={_sequencer.TimerReload}]"); // [LengthCounter={_lengthCounter.Length}]");
                    break;
            }
        }
    }
}
