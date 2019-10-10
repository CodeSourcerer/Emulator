using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public delegate void InterruptingDeviceHandler(object sender, EventArgs e);

    public abstract class InterruptingBusDevice : BusDevice
    {
        public abstract BusDeviceType DeviceType { get; }

        public abstract void Clock(ulong clockCounter);
        public abstract bool Read(ushort addr, out byte data);
        public abstract void Reset();
        public abstract bool Write(ushort addr, byte data);

        public abstract event InterruptingDeviceHandler RaiseInterrupt;
    }
}
