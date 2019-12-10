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
        private const double CLOCK_NTSC_APU = 894886.5;

        private APULengthCounter _lengthCounter;
        private APUSequencer _sequencer;
        private APUVolumeEnvelope _volumeEnvelope;
        private APUSweep _sweepUnit;

        private static byte[] DUTY_CYCLE = 
        {
            0b00000001,
            0b00000011,
            0b00001111,
            0b11111100
        };

        //private static readonly short[] VOLUME_LOOKUP = new short[] {     0,  2184,  4369,  6553,  8738, 10922, 13107, 15291, 
        //                                                              17476, 19660, 21845, 24029, 26214, 28398, 30583, 32000 };
        private static readonly short[] VOLUME_LOOKUP = {     0,  2184,  4369,  6553,  8738, 10922, 13107, 15291,
                                                          17476, 19660, 21845, 24029, 26214, 28398, 30000, 31000 };
        private byte _dutyCycle;
        private int _dutyCycleIndex;
        public short Output { get; private set; }

        public bool Enabled
        {
            set
            {
                if (!value)
                    _lengthCounter.ClearLength();
            }
            get
            {
                return _lengthCounter.Length > 0;
            }
        }

        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
            _volumeEnvelope = new APUVolumeEnvelope();
            _sweepUnit = new APUSweep(channel, _sweepUnit_PeriodUpdate);
            //_sweepUnit.PeriodUpdate += _sweepUnit_PeriodUpdate;
            _dutyCycleIndex = 0;
            _dutyCycle = DUTY_CYCLE[0];
            _lengthCounter = new APULengthCounter(lengthCounterElapsed);
//            _lengthCounter.CounterElapsed += lengthCounterElapsed;
            _lengthCounter.Enabled = true;
            _sequencer = new APUSequencer();
            _sequencer.OnTimerElapsed += sequencer_GenerateWave;
        }

        private void _sweepUnit_PeriodUpdate(object sender, EventArgs e)
        {
            if (!_sweepUnit.MuteChannel)
            {
                _sequencer.TimerReload = _sweepUnit.ChannelPeriod;
                _sequencer.Timer = _sequencer.TimerReload; // ??

                setfreq();
            }
        }

        private void lengthCounterElapsed(object sender, EventArgs e)
        {

        }

        bool _previousDuty;
        double _ramp;
        double _freq;
        DateTime _dtStartApp = DateTime.Now;
        private void sequencer_GenerateWave(object sender, EventArgs e)
        {
            if (!isChannelMuted())
            {
                //bool thisDuty = _dutyCycle.TestBit(_dutyCycleIndex);
                //_ramp = thisDuty == _previousDuty ? (_ramp + 0.10) : 0.10;
                //_previousDuty = thisDuty;
                this.Output = (short)((_dutyCycle.TestBit(_dutyCycleIndex) ? VOLUME_LOOKUP[_volumeEnvelope.Volume] : (short)(-VOLUME_LOOKUP[_volumeEnvelope.Volume])));
                _dutyCycleIndex = (--_dutyCycleIndex) & 7;
                //Output = (short)(getSample(((DateTime.Now - _dtStartApp).TotalSeconds))*1000);
            }
            else
            {
                this.Output = 0;
            }
        }

        private void setfreq()
        {
            _freq = 1789773.0 / (16 * (_sequencer.TimerReload + 1));
        }

        double pi2 = 2 * Math.PI;
        private double getSample(double t)
        {
            double a = 0,
                   b = 0;
            double p = getDutyCycle() * pi2;
            int harmonics = 20;
            //double freq = 1789773.0 / (16 * (_sequencer.TimerReload + 1));
            double c2 = _freq * pi2 * t;
            for (int n = 1; n < harmonics; n++)
            {
                double c = n * c2;
                a += approxsin(c) / n;
                b += approxsin(c - p * n) / n;
            }

            double sample = (2.0 / Math.PI)*(a - b);
            return sample;
        }

        private double approxsin(double t)
        {
            double j = t * 0.15915;
            j = j - (int)j;
            return 20.785 * j * (j - 0.5) * (j - 1.0);
        }

        private double getDutyCycle()
        {
            double duty = 0.0;
            switch (_dutyCycle)
            {
                case 1:
                    duty = 0.125;
                    break;
                case 7:
                    duty = 0.25;
                    break;
                case 15:
                    duty = 0.50;
                    break;
                case 252:
                    duty = 0.75;
                    break;
            }

            return duty;
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

        private ushort _timerTemp;
        public void Write(ushort addr, byte data)
        {
            if (addr == 0x4000 || addr == 0x4004)
            {
                _lengthCounter.Halt = data.TestBit(5);
                _dutyCycle = DUTY_CYCLE[data >> 6];
                _volumeEnvelope.ConstantVolume = data.TestBit(4);
                _volumeEnvelope.Volume = (byte)(data & 0x0F);
                if (!_volumeEnvelope.ConstantVolume)
                    _sweepUnit.ChannelPeriod = _volumeEnvelope.Volume;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [Duty={data >> 6}] [Halt={_lengthCounter.Halt}] [ConstantVolume={_volumeEnvelope.ConstantVolume}] [Volume/Period={_volumeEnvelope.Volume}]");
            }
            else if (addr == 0x4001 || addr == 0x4005)
            {
                _sweepUnit.Enabled = data.TestBit(7);
                _sweepUnit.Negate = data.TestBit(3);
                _sweepUnit.DividerPeriod = ((data >> 4) & 7) + 1;
                _sweepUnit.ShiftCount = (byte)(data & 7);
                _sweepUnit.Reload = true;
                Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [SweepEnabled={_sweepUnit.Enabled}] [DividerPeriod={_sweepUnit.DividerPeriod}] [Negate={_sweepUnit.Negate}] [ShiftCount={_sweepUnit.ShiftCount}]");
            }
            // Pulse channel 1 & 2 timer low bits
            else if (addr == 0x4002 || addr == 0x4006)
            {
                _timerTemp &= 0xFF00;
                _timerTemp |= data;
            }
            // Pulse channel 1 & 2 length counter load and timer high bits
            else if (addr == 0x4003 || addr == 0x4007)
            {
                _lengthCounter.LoadLength((byte)(data >> 3));
                _timerTemp &= 0x00FF;
                _timerTemp |= (ushort)((data & 0x07) << 8);
                _sequencer.TimerReload = _timerTemp;
                _dutyCycleIndex = 0;
                _volumeEnvelope.Start = true;
                //Log.Debug($"Pulse channel {((addr & 0x04) >> 2) + 1} written. [LinearLength={_lengthCounter.Length}] [TimerReload={_sequencer.TimerReload}]");

                setfreq();
            }
        }

        private bool isChannelMuted()
        {
            bool isMuted = (_lengthCounter.Length == 0 ||
                            _volumeEnvelope.Volume == 0 ||
                            //Output == 0 ||
                            _sequencer.TimerReload < 8 ||
                            _sweepUnit.MuteChannel);

            return isMuted;
        }
    }
}
