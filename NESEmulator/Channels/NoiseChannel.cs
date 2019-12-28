using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using NESEmulator.APU;
using NESEmulator.Util;

namespace NESEmulator.Channels
{
    public class NoiseChannel : Channel
    {
        private static ILog Log = LogManager.GetLogger(typeof(PulseChannel));
        private static readonly ushort[] noise_period = { 4, 8, 16, 32, 64, 96, 128, 160, 202, 254, 380, 508, 762, 1016, 2034, 4068 };

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

        public ushort ShiftRegister { get; set; }
        public bool ShiftMode { get; set; }

        private APULengthCounter _lengthCounter;
        private APUVolumeEnvelope _volumeEnvelope;
        private APUSequencer _sequencer;

        public NoiseChannel()
        {
            _lengthCounter = new APULengthCounter(lengthCounter_Elapsed);
            _volumeEnvelope = new APUVolumeEnvelope();
            _sequencer = new APUSequencer();
            _sequencer.OnTimerElapsed += sequencer_generateWave;
            ShiftRegister = 1;
        }

        private void lengthCounter_Elapsed(object sender, EventArgs e)
        { }

        private void sequencer_generateWave(object sender, EventArgs e)
        {

        }

        public void Clock(ulong clockCycles)
        {
            if (clockCycles % 6 == 0)
            {
                int feedback_bit = ShiftRegister >> (ShiftMode ? 6 : 1) & 0x0001;
                feedback_bit ^= ShiftRegister & 0x0001;
                ShiftRegister = (ushort)(feedback_bit << 14 | ShiftRegister >> 1);
                if (_lengthCounter.Length == 0 || ((ShiftRegister & 0x0001) == 1))
                {
                    Output = 0;
                }
                else
                {
                    Output = _volumeEnvelope.Volume;
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
            _lengthCounter.Clock();
        }

        /// <summary>
        /// Called from APU when FrameCounter detects we are on a quarter frame.
        /// </summary>
        /// <remarks>
        /// This is where we update the volume envelope.
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
            if (addr == 0x400C)
            {
                _lengthCounter.Halt = data.TestBit(5);
                _volumeEnvelope.ConstantVolume = data.TestBit(4);
                _volumeEnvelope.Volume = (byte)(data & 0x0F);
                //Log.Debug($"Noise channel written. [Halt={_lengthCounter.Halt}] [ConstantVolume={_volumeEnvelope.ConstantVolume}] [Volume/Period={_volumeEnvelope.Volume}]");
            }
            else if (addr == 0x400E)
            {
                ShiftMode = data.TestBit(7);
                _sequencer.TimerReload = noise_period[data & 0x0F];
                //Log.Debug($"Noise channel written. [ShiftMode={ShiftMode}] [Timer={_sequencer.TimerReload}]");
            }
            else if (addr == 0x400F)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _volumeEnvelope.Start = true;
                //Log.Debug($"Noise channel written. [Length={_lengthCounter.Length}]");
            }
        }
    }
}
