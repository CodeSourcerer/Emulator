using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using log4net;
using NESEmulator.APU;
using NESEmulator.Channels;
using NESEmulator.Util;

namespace NESEmulator
{
    public class CS2A03 : InterruptingBusDevice
    {
        public BusDeviceType DeviceType => BusDeviceType.APU;
        public event InterruptingDeviceHandler RaiseInterrupt;
        public const int SOUND_BUFFER_SIZE_MS   = 20;

        private static ILog Log = LogManager.GetLogger(typeof(CS2A03));
        private const float  CLOCK_NTSC_HZ      = 1789773.0f;
        private const double CLOCK_NTSC_APU     = 894886.5;
        private const int   SAMPLE_FREQUENCY    = 20;

        private const ushort ADDR_PULSE1_LO     = 0x4000;
        private const ushort ADDR_PULSE1_HI     = 0x4003;
        private const ushort ADDR_PULSE2_LO     = 0x4004;
        private const ushort ADDR_PULSE2_HI     = 0x4007;
        private const ushort ADDR_TRI_LO        = 0x4008;
        private const ushort ADDR_TRI_HI        = 0x400B;
        private const ushort ADDR_NOISE_LO      = 0x400C;
        private const ushort ADDR_NOISE_HI      = 0x400F;
        private const ushort ADDR_DMC_LO        = 0x4010;
        private const ushort ADDR_DMC_HI        = 0x4013;
        private const ushort ADDR_STATUS        = 0x4015;
        private const ushort ADDR_FRAME_COUNTER = 0x4017;

        private Bus _bus;

        private ushort          _apuClockCounter;
        private uint            _cpuClockCounter;
        private APUFrameCounter _frameCounter;
        private Channel[]       _audioChannels;
        private PulseChannel    _pulseChannel1;
        private PulseChannel    _pulseChannel2;
        private TriangleChannel _triangleChannel;
        private NoiseChannel    _noiseChannel;
        private DMCChannel      _dmcChannel;

        private const int AUDIO_BUFFER_SIZE     = (int)(44100 * (SOUND_BUFFER_SIZE_MS / 1000.0));
        private bool    _audioReadyToPlay;
        private int     _audioBufferPtr;
        private short[] _audioBuffer;

        public CS2A03()
        {
            _audioBuffer     = new short[AUDIO_BUFFER_SIZE];
            _pulseChannel1   = new PulseChannel(1);
            _pulseChannel2   = new PulseChannel(2);
            _triangleChannel = new TriangleChannel();
            _noiseChannel    = new NoiseChannel();
            _dmcChannel      = new DMCChannel(this);
            _audioChannels   = new Channel[] { _pulseChannel1, _pulseChannel2, _triangleChannel, _noiseChannel, _dmcChannel };

            _frameCounter    = new APUFrameCounter(_audioChannels, this);
        }

        public void ConnectBus(Bus bus)
        {
            _bus = bus;
        }

        public void Clock(ulong clockCounter)
        {
            // Clock all audio channels, letting them determine whether or not to actually do something or not
            foreach (var audioChannel in _audioChannels)
            {
                audioChannel.Clock(clockCounter);
            }

            if (_frameCounter.FrameInterrupt)
                IRQ();

            // APU clocks every other CPU cycle
            if (clockCounter % 6 == 0)
            {
                ++_apuClockCounter;
                if (_apuClockCounter % SAMPLE_FREQUENCY == 0)
                {
                    if (_audioBufferPtr < AUDIO_BUFFER_SIZE)
                    {
                        _audioBuffer[_audioBufferPtr++] = GetMixedAudioSample();
                    }
                    else
                    {
                        _audioReadyToPlay = true;
                    }
                }
            }

            // Clock frame counter every CPU cycle
            if (clockCounter % 3 == 0)
            {
                ++_cpuClockCounter;
                _frameCounter.Clock(_cpuClockCounter);
            }
        }

        public bool Read(ushort addr, out byte data)
        {
            bool dataRead = false;
            data = 0x00;

            if (addr >= ADDR_PULSE1_LO && addr <= ADDR_PULSE2_HI)
            {
                dataRead = true;
                Log.Debug($"Pulse channel address read [addr={addr:X2}]");
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataRead = true;
                data = _triangleChannel.Read(addr);
                Log.Debug($"Triangle channel address read [addr={addr:X2}]");
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataRead = true;
                Log.Debug($"Noise channel address read [addr={addr:X2}]");
            }
            else if (addr >= ADDR_DMC_LO && addr <= ADDR_DMC_HI)
            {
                dataRead = true;
                data = _dmcChannel.Read(addr);
                Log.Debug($"DMC channel address read [addr={addr:X2}]");
            }
            else if (addr == ADDR_STATUS)
            {
                dataRead = true;
                data = (byte)((_dmcChannel.InterruptFlag ? 0 : 1) << 7 |
                              (_frameCounter.FrameInterrupt ? 0 : 1) << 6);
                _frameCounter.FrameInterrupt = false;
                Log.Debug("Status register read");
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataRead = true;
                Log.Debug("Frame counter read");
            }

            return dataRead;
        }

        public void Reset()
        {
            _apuClockCounter = 0;
            Write(ADDR_STATUS, 0x00);
        }

        public bool Write(ushort addr, byte data)
        {
            bool dataWritten = false;

            if (addr >= ADDR_PULSE1_LO && addr <= ADDR_PULSE1_HI)
            {
                _pulseChannel1.Write(addr, data);
                dataWritten = true;
                //Log.Debug($"Pulse channel 1 address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_PULSE2_LO && addr <= ADDR_PULSE2_HI)
            {
                _pulseChannel2.Write(addr, data);
                dataWritten = true;
                //Log.Debug($"Pulse channel 2 address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataWritten = true;
                _triangleChannel.Write(addr, data);
                //Log.Debug($"Triangle channel address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataWritten = true;
                _noiseChannel.Write(addr, data);
                //Log.Debug($"Noise channel address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_DMC_LO && addr <= ADDR_DMC_HI)
            {
                dataWritten = true;
                _dmcChannel.Write(addr, data);
                //Log.Debug($"DMC channel address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr == ADDR_STATUS)
            {
                dataWritten = true;
                _pulseChannel1.Enabled = data.TestBit(0);
                _pulseChannel2.Enabled = data.TestBit(1);
                _triangleChannel.Enabled = data.TestBit(2);
                _noiseChannel.Enabled = data.TestBit(3);
                _dmcChannel.Enabled = data.TestBit(4);
                _dmcChannel.InterruptFlag = false;
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataWritten = true;
                _frameCounter.Reset();
                _frameCounter.InterruptInhibit = data.TestBit(6);
                if (_frameCounter.InterruptInhibit)
                    _frameCounter.FrameInterrupt = false;

                if (data.TestBit(7) == false)
                {
                    _frameCounter.Mode = SequenceMode.FourStep;
                }
                else
                {
                    _frameCounter.Mode = SequenceMode.FiveStep;
                }
                Log.Debug($"Frame counter written [data={data:X2}]");
            }

            return dataWritten;
        }

        public byte ReadBus(ushort addr)
        {
            byte dataRead = 0x00;

            if (_bus != null)
            {
                dataRead = _bus.Read(addr);
            }

            return dataRead;
        }

        public void WriteBus(ushort addr, byte data)
        {
            if (_bus != null)
            {
                _bus.Write(addr, data);
            }
        }

        /// <summary>
        /// This allows the APU to signal an interrupt to the CPU
        /// </summary>
        public void IRQ()
        {
            RaiseInterrupt?.Invoke(this, new InterruptEventArgs(InterruptType.IRQ));
        }

        public bool DMAInProgress() => ((CS6502)_bus?.GetDevice(BusDeviceType.CPU)).DMATransfer;

        [SuppressMessage("Potential Code Quality Issues",
                         "RECS0018:Comparison of floating point numbers with equality operator",
                         Justification = "Condition should only be met when output is exactly 0.0 to avoid divide by 0 errors")]
        public short GetMixedAudioSample()
        {
            short pulse = (short)(_pulseChannel1.Output + _pulseChannel2.Output);
            double pulse_out = pulse == 0 ? 0 : 95.88 / (8128.0 / pulse + 100);
            double tnd = _triangleChannel.Output / 8227.0 + (_noiseChannel.Output / 12241.0) + (_dmcChannel.Output / 22638.0);
            double tnd_out = tnd == 0 ? 0 : 159.79 / (1 / tnd + 100);

            short mixedOutput = (short)((pulse_out + tnd_out) * short.MaxValue);
            //short mixedOutput = (short)((_dmcChannel.Output / 128.0) * short.MaxValue);
            return mixedOutput;
        }

        public short[] ReadAndResetAudio()
        {
            _audioReadyToPlay = false;
            _audioBufferPtr = 0;
            return _audioBuffer;
        }

        public bool IsAudioBufferReadyToPlay() => _audioReadyToPlay;

        public void InitiateDMCSampleFetch(byte cyclesToStall, ushort sampleAddr)
        {
            CS6502 cpu = (CS6502)_bus?.GetDevice(BusDeviceType.CPU);
            cpu.ExternalMemoryReader.MemoryPtr = sampleAddr;
            cpu.ExternalMemoryReader.BeginRead(this);
        }

        public MemoryReader GetMemoryReader()
        {
            return _bus?.GetCPU().ExternalMemoryReader;
        }
    }
}
