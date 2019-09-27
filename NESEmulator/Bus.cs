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
            BusDevice ppu = _busDeviceList.Find((bd) => bd.DeviceType == BusDeviceType.PPU);
            if (ppu != null)
                ((CS2C02)ppu).ConnectCartridge(cartridge);
        }

        public void clock()
        {
            BusDevice ppu = _busDeviceList.Find((bd) => bd.DeviceType == BusDeviceType.PPU);
            if (ppu != null)
                ((CS2C02)ppu).clock();

        }
    }
}
