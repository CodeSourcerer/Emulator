using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public enum InterruptType { NMI, IRQ }

    public abstract class InterruptableBusDevice : BusDevice
    {
        public abstract BusDeviceType DeviceType { get; }

        public abstract void Clock(ulong clockCounter);
        public abstract bool Read(ushort addr, out byte data);
        public abstract void Reset();
        public abstract bool Write(ushort addr, byte data);

        public abstract void HandleInterrupt(object sender, InterruptEventArgs e);
    }
}
