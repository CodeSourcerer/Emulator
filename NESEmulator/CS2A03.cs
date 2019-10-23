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

        private const ushort ADDR_PULSE_LO = 0x4000;
        private const ushort ADDR_PULSE_HI = 0x4007;
        private const ushort ADDR_TRI_LO = 0x4008;
        private const ushort ADDR_TRI_HI = 0x400B;
        private const ushort ADDR_NOISE_LO = 0x400C;
        private const ushort ADDR_NOISE_HI = 0x400F;
        private const ushort ADDR_DMC_LO = 0x4010;
        private const ushort ADDR_DMC_HI = 0x4013;
        private const ushort ADDR_STATUS = 0x4015;
        private const ushort ADDR_FRAME_COUNTER = 0x4017;

        private APUFrameCounter _frameCounter;

        private ushort _apuClockCounter;
        private uint _cpuClockCounter;
        private byte _sequenceStep;

        public CS2A03()
        {
            Channel[] audioChannels = { new TriangleChannel() };
            _frameCounter = new APUFrameCounter(audioChannels, this);
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

            if (addr >= ADDR_PULSE_LO && addr <= ADDR_PULSE_HI)
            {
                dataRead = true;
            }
            else if (addr >= ADDR_TRI_LO && addr <= ADDR_TRI_HI)
            {
                dataRead = true;
            }
            else if (addr >= ADDR_NOISE_LO && addr <= ADDR_NOISE_HI)
            {
                dataRead = true;
            }
            else if (addr >= ADDR_DMC_LO && addr <= ADDR_DMC_HI)
            {
                dataRead = true;
            }
            else if (addr == ADDR_STATUS)
            {
                dataRead = true;
            }
            else if (addr == ADDR_FRAME_COUNTER)
            {
                dataRead = true;
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
            throw new NotImplementedException();
        }

        public void IRQ()
        {
            this.RaiseInterrupt?.Invoke(this, new InterruptEventArgs(InterruptType.IRQ));
        }
    }
}
