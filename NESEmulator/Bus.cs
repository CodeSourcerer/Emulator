﻿using System;
using System.Linq;
using System.Collections.Generic;
using log4net;

namespace NESEmulator
{
    /// <summary>
    /// Represents the main bus on the NES
    /// </summary>
    public class Bus : IBus
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Bus));

        private List<BusDevice> _busDeviceList;
        private ulong _systemClockCounter;
        private bool _cartConflicts;

        private CS6502 _cpu;

        public Bus(BusDevice[] busDevices)
        {
            _busDeviceList = new List<BusDevice>(busDevices);

            List<InterruptingBusDevice> interruptingDevices = _busDeviceList.FindAll((bd) => bd is InterruptingBusDevice)
                                                                            .ConvertAll((bd) => (InterruptingBusDevice)bd);
            var interruptableBusDevices = getInterruptableDevices();

            foreach (var device in interruptableBusDevices)
            {
                foreach (var intDevice in interruptingDevices)
                {
                    intDevice.RaiseInterrupt += device.HandleInterrupt;
                    Log.Debug($"Added device {intDevice.DeviceType} as interrupting device");
                }
            }

            _cartConflicts = ((Cartridge)GetDevice(BusDeviceType.CART))?.HasBusConflicts ?? false;
            Log.Debug($"Cartridge bus conflicts: {_cartConflicts}");
        }

        private IEnumerable<InterruptableBusDevice> getInterruptableDevices()
        {
            var interruptableBusDevices = from device in _busDeviceList
                                          where device is InterruptableBusDevice
                                          select (InterruptableBusDevice)device;

            return interruptableBusDevices;
        }

        public byte Read(ushort addr, bool readOnly = false)
        {
            byte data = 0;

            foreach (BusDevice device in _busDeviceList)
            {
                if (device.Read(addr, out data))
                    break;
            }

            return data;
        }

        private bool _cartWrite;
        public void Write(ushort addr, byte data)
        {
            foreach (BusDevice device in _busDeviceList)
            {
                if (device.Write(addr, data))
                {
                    // For mappers that have bus conflicts (two values written to the bus at once), this
                    // allows the CPU to "win" on a write over the cartridge.
                    if (_cartConflicts && device.DeviceType == BusDeviceType.CART)
                    {
                        _cartWrite = true;
                        continue;
                    }

                    if (_cartConflicts && device.DeviceType == BusDeviceType.CPU && _cartWrite)
                    {
                        Log.Debug($"CPU write used in BUS conflict");
                    }
                    break;
                }
            }
        }

        public void Reset()
        {
            _busDeviceList.ForEach(device => device.Reset());

            _systemClockCounter = 0;
        }

        public void InsertCartridge(Cartridge cartridge)
        {
            _busDeviceList.Insert(0, cartridge);
            CS2C02 ppu = GetPPU();
            ppu?.ConnectCartridge(cartridge);

            var interruptableBusDevices = getInterruptableDevices();

            foreach (var device in interruptableBusDevices)
            {
                cartridge.RaiseInterrupt += device.HandleInterrupt;
                Log.Debug($"Added device {cartridge.DeviceType} as interrupting device");
            }

            _cartConflicts = cartridge.HasBusConflicts;

            if (cartridge.UsesScanlineCounter)
            {
                ppu.DrawSprites += ((IScanlineCounterMapper)cartridge.mapper).OnSpriteFetch;
            }

            Log.Debug($"Cartridge bus conflicts: {_cartConflicts}");
        }

        public void clock()
        {
            // Clocking. The heart and soul of an emulator. The running frequency is controlled by whatever calls
            // this function. So here we "divide" the clock as necessary and call the peripheral devices Clock()
            // function at the correct times.

            _busDeviceList.ForEach(device => device.Clock(_systemClockCounter));

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

        public CS6502 GetCPU()
        {
            _cpu = _cpu ?? (CS6502)GetDevice(BusDeviceType.CPU);
            return _cpu;
        }
    }
}
