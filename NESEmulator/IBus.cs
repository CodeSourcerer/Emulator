using System;
namespace NESEmulator
{
    public interface IBus
    {
        byte Read(ushort addr, bool readOnly = false);
        void Write(ushort addr, byte data);
        void Reset();
        void PowerOn();
    }
}
