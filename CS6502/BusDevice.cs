using System;
namespace CS6502
{
    public interface BusDevice
    {
        BusDeviceType DeviceType { get; }

        bool Write(ushort addr, byte data);
        bool Read(ushort addr, out byte data);
        void Reset();
    }
}
