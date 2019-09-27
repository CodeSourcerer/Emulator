using System;
using System.Collections.Generic;

namespace NESEmulator
{
    /// <summary>
    /// Represents the main bus on the NES
    /// </summary>
    public class Bus : IBus
    {
        private List<BusDevice> _busDeviceList;
        private long _systemClockCounter;

        public Bus(BusDevice[] busDevices)
        {
            _busDeviceList = new List<BusDevice>(busDevices);
        }

        public byte Read(ushort addr, bool readOnly = false)
        {
            byte data = 0;

            foreach(BusDevice device in _busDeviceList)
            {
                if (device.Read(addr, out data))
                    break;
            }

            return data;
        }

        public void Write(ushort addr, byte data)
        {
            foreach(BusDevice device in _busDeviceList)
            {
                if (device.Write(addr, data))
                    break;
            }
        }

        public void Reset()
        {
            foreach (var device in _busDeviceList)
                device.Reset();

            _systemClockCounter = 0;
        }

        public void InsertCartridge(Cartridge cartridge)
        {
            _busDeviceList.Insert(0, cartridge);
            BusDevice ppu = GetPPU();
            if (ppu != null)
                ((CS2C02)ppu).ConnectCartridge(cartridge);
        }

        public void clock()
        {
            BusDevice ppu = GetPPU();
            if (ppu != null)
                ppu.Clock();

            // The CPU runs 3 times slower than the PPU so we only call its
            // clock() function every 3 times this function is called. We
            // have a global counter to keep track of this.
            if (_systemClockCounter % 3 == 0)
            {
                var cpu = _busDeviceList.Find((bd) => bd.DeviceType == BusDeviceType.CPU);
                cpu.Clock();
            }
        }

        public BusDevice GetPPU()
        {
            return _busDeviceList.Find((bd) => bd.DeviceType == BusDeviceType.PPU);
        }
    }
}
