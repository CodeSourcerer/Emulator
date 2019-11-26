using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public class CS2A03 : InterruptingBusDevice
    {
        public override BusDeviceType DeviceType => BusDeviceType.APU;
        public override event InterruptingDeviceHandler RaiseInterrupt;

        private const float CLOCK_NTSC_MHZ = 1.789773f;

        private const ushort ADDR_PULSE1_LO = 0x4000;
        private const ushort ADDR_PULSE1_HI = 0x4003;
        private const ushort ADDR_PULSE2_HI = 0x4007;
        private const ushort ADDR_TRI_LO   = 0x4008;
        private const ushort ADDR_TRI_HI   = 0x400B;
        private const ushort ADDR_NOISE_LO = 0x400C;
        private const ushort ADDR_NOISE_HI = 0x400F;
        private const ushort ADDR_DMC_LO   = 0x4010;
        private const ushort ADDR_DMC_HI   = 0x4013;
        private const ushort ADDR_STATUS   = 0x4015;
        private const ushort ADDR_FRAME_COUNTER = 0x4017;

        private Bus _bus;

        private APUFrameCounter _frameCounter;

        private ushort _apuClockCounter;
        private uint _cpuClockCounter;
        private byte _sequenceStep;
        private PulseChannel _pulseChannel1;
        private TriangleChannel _triangleChannel;
        private DMCChannel _dmcChannel;

        public CS2A03()
        {
            _pulseChannel1   = new PulseChannel(1);
            _triangleChannel = new TriangleChannel();
            _dmcChannel      = new DMCChannel(this);
            Channel[] audioChannels = { _pulseChannel1, _triangleChannel, _dmcChannel };

            _frameCounter = new APUFrameCounter(audioChannels, this);
        }

        public void ConnectBus(Bus bus)
        {
            this._bus = bus;
        }

        public override void Clock(ulong clockCounter)
        {
            if (clockCounter % 6 == 0)
            {
                ++_apuClockCounter;
            }
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
                Console.WriteLine("Pulse channel address read: {0:X2}", addr);
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataRead = true;
                data = _triangleChannel.Read(addr);
                Console.WriteLine("Triangle channel address read: {0:X2}", addr);
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataRead = true;
                Console.WriteLine("Noise channel address read: {0:X2}", addr);
            }
            else if (addr >= ADDR_DMC_LO && addr <= ADDR_DMC_HI)
            {
                dataRead = true;
                data = _dmcChannel.Read(addr);
                Console.WriteLine("DMC channel address read: {0:X2}", addr);
            }
            else if (addr == ADDR_STATUS)
            {
                dataRead = true;
                Console.WriteLine("Status register read");
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataRead = true;
                Console.WriteLine("Frame counter read");
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
                Console.WriteLine("Pulse channel 1 address written: {0:X2}; data: {1:X2}", addr, data);
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataWritten = true;
                _triangleChannel.Write(addr, data);
                Console.WriteLine("Triangle channel address written: {0:X2}; data: {1:X2}", addr, data);
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataWritten = true;
                Console.WriteLine("Noise channel address written: {0:X2}; data: {1:X2}", addr, data);
            }
            else if (addr >= ADDR_DMC_LO && addr <= ADDR_DMC_HI)
            {
                dataWritten = true;
                _dmcChannel.Write(addr, data);
                Console.WriteLine("DMC channel address written: {0:X2}; data: {1:X2}", addr, data);
            }
            else if (addr == ADDR_STATUS)
            {
                dataWritten = true;
                Console.WriteLine("Status register written with data: {0:X2}", data);
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataWritten = true;
                Console.WriteLine("Frame counter written; data: {0:X2}", data);
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
    }
}
