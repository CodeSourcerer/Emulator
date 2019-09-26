using System;

namespace CS6502
{
    public class Bus : IBus
    {
        private BusDevice[] _busDevices;
        private long _systemClockCounter;

        public Bus(BusDevice[] busDevices)
        {
            this._busDevices = busDevices;
        }

        public byte Read(ushort addr, bool readOnly = false)
        {
            byte data = 0;

            foreach(BusDevice device in _busDevices)
            {
                if (device.Read(addr, out data))
                    break;
            }

            return data;
        }

        public void Write(ushort addr, byte data)
        {
            foreach(BusDevice device in _busDevices)
            {
                if (device.Write(addr, data))
                    break;
            }
        }
    }
}
