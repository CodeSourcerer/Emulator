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
        private ulong _systemClockCounter;

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
            // Clocking. The heart and soul of an emulator. The running frequency is controlled by whatever calls
            // this function. So here we "divide" the clock as necessary and call the peripheral devices Clock()
            // function at the correct times.

            // The fastest clock frequency the digital system cares about is equivalent to the PPU clock. So the
            // PPU is clocked each time this function is called.
            CS2C02 ppu = GetPPU();
            if (ppu != null)
                ppu.Clock();

            // The CPU runs 3 times slower than the PPU so we only call its Clock() function every 3 times this
            // function is called. We have a global counter to keep track of this.
            var cpu = GetDevice(BusDeviceType.CPU);
            if (cpu != null && _systemClockCounter % 3 == 0)
            {
                cpu.Clock();
            }

            // The PPU is capable of emitting an interrupt to indicate the vertical blanking period has been
            // entered. If it has, we need to send that irq to the CPU.
            if (ppu != null && cpu != null)
            {
                if (ppu.NMI)
                {
                    ppu.NMI = false;
                    ((CS6502)cpu).NMI();
                }
            }

            _systemClockCounter++;
        }

        public BusDevice GetDevice(BusDeviceType device)
        {
            return _busDeviceList.Find((bd) => bd.DeviceType == device);
        }

        public CS2C02 GetPPU()
        {
            return (CS2C02)GetDevice(BusDeviceType.PPU);
        }
    }
}
