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

            List<InterruptingBusDevice> interruptingDevices = _busDeviceList.FindAll((bd) => bd is InterruptingBusDevice)
                                                                            .ConvertAll((bd) => (InterruptingBusDevice)bd);
            foreach (var device in _busDeviceList)
            {
                if (device is InterruptableBusDevice)
                {
                    foreach (var intDevice in interruptingDevices)
                        intDevice.RaiseInterrupt += ((InterruptableBusDevice)device).HandleInterrupt;
                }
            }
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
            CS2C02 ppu = GetPPU();
            if (ppu != null)
                ppu.ConnectCartridge(cartridge);
        }

        public void clock()
        {
            // Clocking. The heart and soul of an emulator. The running frequency is controlled by whatever calls
            // this function. So here we "divide" the clock as necessary and call the peripheral devices Clock()
            // function at the correct times.

            foreach (BusDevice busDevice in _busDeviceList)
            {
                busDevice.Clock(_systemClockCounter);
            }

            CS2C02 ppu = GetPPU();
            var cpu = GetDevice(BusDeviceType.CPU);

            // The PPU is capable of emitting an interrupt to indicate the vertical blanking period has been
            // entered. If it has, we need to send that irq to the CPU.
            //if (ppu != null && cpu != null)
            //{
            //    if (ppu.NMI)
            //    {
            //        ppu.NMI = false;
            //        ((CS6502)cpu).NMI();
            //    }
            //}

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
