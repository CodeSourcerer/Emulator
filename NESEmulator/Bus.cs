using System;
using System.Linq;
using System.Collections.Generic;

namespace NESEmulator
{
    /// <summary>
    /// Represents the main bus on the NES
    /// </summary>
    public class Bus : IBus
    {
        public short CurrentAudioSample { get; private set; }

        private List<BusDevice> _busDeviceList;
        private ulong _systemClockCounter;
        private int _audioSampleRate;
        private double _audioTimePerSystemSample;
        private double _audioTimePerNESClock = 1.0 / 5369318.0;
        private double _audioTime = 0;

        private CS2A03 _apu;
        private CS2C02 _ppu;

        public Bus(BusDevice[] busDevices, int audioSampleRate)
        {
            _audioSampleRate = audioSampleRate;
            _audioTimePerSystemSample = 1.0 / _audioSampleRate;

            _busDeviceList = new List<BusDevice>(busDevices);

            List<InterruptingBusDevice> interruptingDevices = _busDeviceList.FindAll((bd) => bd is InterruptingBusDevice)
                                                                            .ConvertAll((bd) => (InterruptingBusDevice)bd);
            var interruptableBusDevices = from device in _busDeviceList
                                          where device is InterruptableBusDevice
                                          select device;
            foreach (var device in interruptableBusDevices)
            {
                foreach (var intDevice in interruptingDevices)
                    intDevice.RaiseInterrupt += ((InterruptableBusDevice)device).HandleInterrupt;
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
            foreach (BusDevice device in _busDeviceList)
            {
                if (device.Write(addr, data))
                    break;
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
        }

        public bool clock()
        {
            // Clocking. The heart and soul of an emulator. The running frequency is controlled by whatever calls
            // this function. So here we "divide" the clock as necessary and call the peripheral devices Clock()
            // function at the correct times.

            _busDeviceList.ForEach(device => device.Clock(_systemClockCounter));

            _systemClockCounter++;

            bool isAudioSampleReady = false;
            _audioTime += _audioTimePerNESClock;

            if (_audioTime >= _audioTimePerSystemSample)
            {
                _audioTime -= _audioTimePerSystemSample;
                CurrentAudioSample = GetAPU().GetMixedAudioSample();
                isAudioSampleReady = true;
            }

            return isAudioSampleReady;
        }

        public BusDevice GetDevice(BusDeviceType device)
        {
            return _busDeviceList.Find((bd) => bd.DeviceType == device);
        }

        public CS2C02 GetPPU()
        {
            _ppu = (_ppu ?? (CS2C02)GetDevice(BusDeviceType.PPU));
            return _ppu;
        }

        private CS2A03 GetAPU()
        {
            _apu = (_apu ?? (CS2A03)GetDevice(BusDeviceType.APU));
            return _apu;
        }
    }
}
