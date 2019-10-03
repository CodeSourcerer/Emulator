using System;
namespace NESEmulator
{
    public struct PPUControl
    {
        public byte reg;

        public bool NameTableX
        {
            get
            {
                return (byte)(reg & 0x01) == 1;
            }
            set
            {
                reg |= (byte)(value ? 1 : 0);
            }
        }

        public bool NameTableY
        {
            get
            {
                return ((byte)((reg >> 1) & 0x01)) == 1;
            }
        }
    }
}
