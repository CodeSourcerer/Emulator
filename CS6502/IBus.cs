using System;
namespace CS6502
{
    public interface IBus
    {
        byte Read(ushort addr, bool readOnly = false);
        void Write(ushort addr, byte data);
    }
}
