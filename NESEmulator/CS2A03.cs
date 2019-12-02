using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using log4net;

namespace NESEmulator
{
    public class CS2A03 : InterruptingBusDevice
    {
        public override BusDeviceType DeviceType => BusDeviceType.APU;
        public override event InterruptingDeviceHandler RaiseInterrupt;

        private static ILog Log = LogManager.GetLogger(typeof(CS2A03));
        private const float CLOCK_NTSC_MHZ = 1.789773f;
        private const int SAMPLE_SIZE = 20;

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

        private APUFrameCounter _frameCounter;

        private ushort _apuClockCounter;
        private uint _cpuClockCounter;
        private byte _sequenceStep;
        private Channel[] _audioChannels;
        private PulseChannel _pulseChannel1;
        private PulseChannel _pulseChannel2;
        private TriangleChannel _triangleChannel;
        private DMCChannel _dmcChannel;

        private List<short> _audioBuffer;

        public List<short> AudioBuffer
        {
            get { return _audioBuffer; }
        }

        public CS2A03()
        {
            _audioBuffer = new List<short>(4500);
            _pulseChannel1   = new PulseChannel(1);
            _pulseChannel2   = new PulseChannel(2);
            _triangleChannel = new TriangleChannel();
            _dmcChannel      = new DMCChannel(this);
            _audioChannels   = new Channel[] { _pulseChannel1, _pulseChannel2, _triangleChannel, _dmcChannel };

            _frameCounter = new APUFrameCounter(_audioChannels, this);
        }

        public void ConnectBus(Bus bus)
        {
            this._bus = bus;
        }

        public override void Clock(ulong clockCounter)
        {
            // Clock all audio channels, letting them determine whether or not to actually do something or not
            foreach (var audioChannel in _audioChannels)
            {
                audioChannel.Clock(clockCounter);
            }

            // APU clocks every other CPU cycle
            if (clockCounter % 6 == 0)
            {
                ++_apuClockCounter;
                if (_apuClockCounter % SAMPLE_SIZE == 0)
                {
                    _audioBuffer.Add(GetMixedAudioSample());
                }
            }

            // Clock frame counter every CPU cycle
            if (clockCounter % 3 == 0)
            {
                ++_cpuClockCounter;
                _frameCounter.Clock(_cpuClockCounter);
            }
        }

        public override bool Read(ushort addr, out byte data)
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
                Log.Debug("Status register read");
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataRead = true;
                Log.Debug("Frame counter read");
            }

            return dataRead;
        }

        public override void Reset()
        {
            _apuClockCounter = 0;
            _sequenceStep = 0;
        }

        public override bool Write(ushort addr, byte data)
        {
            bool dataWritten = false;

            if (addr >= ADDR_PULSE1_LO && addr <= ADDR_PULSE1_HI)
            {
                _pulseChannel1.Write(addr, data);
                dataWritten = true;
                Log.Debug($"Pulse channel 1 address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_PULSE2_LO && addr <= ADDR_PULSE2_HI)
            {
                _pulseChannel2.Write(addr, data);
                dataWritten = true;
                Log.Debug($"Pulse channel 2 address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataWritten = true;
                _triangleChannel.Write(addr, data);
                Log.Debug($"Triangle channel address written [addr={addr:X2}] [data={data:X2}]");
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataWritten = true;
                Log.Debug($"Noise channel address written [addr={addr:X2}] [data={data:X2}]");
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
                Log.Debug($"Status register written [data={data:X2}]");
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataWritten = true;
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

        public void IRQ()
        {
            this.RaiseInterrupt?.Invoke(this, new InterruptEventArgs(InterruptType.IRQ));
        }

        public short GetMixedAudioSample()
        {
            short pulse1 = this._pulseChannel1.GetOutput();
            short pulse2 = this._pulseChannel2.GetOutput();
            long average = (pulse1 + pulse2) / 2;
            return (short)(average);
        }

        public short[] GetCurrentAudioSample()
        {
            // dump this for now
            this._pulseChannel2.EmptyBuffer();

            return this._pulseChannel1.EmptyBuffer();
        }
    }
}
