using System;
namespace NESEmulator
{
    public interface BusDevice
    {
        BusDeviceType DeviceType { get; }

        bool Write(ushort addr, byte data);
        bool Read(ushort addr, out byte data);
        void Reset();
        void Clock(ulong clockCounter);
        void PowerOn();
    }
}
