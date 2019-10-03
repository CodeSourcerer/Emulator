using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public struct PPULoopyRegister
    {
        public ushort reg;

        public ushort CoarseX
        {
            get
            {
                return (ushort)(reg & 0x1F);
            }
            set
            {
                reg |= (ushort)(value & 0x1F);
            }
        }

        public ushort CoarseY
        {
            get
            {
                return (ushort)((reg & 0x3E0) >> 5);
            }
            set
            {
                reg |= (ushort)((value & 0x1F) << 5);
            }
        }

        public bool NameTableX
        {
            get
            {
                return reg.TestBit(10);
            }
            set
            {
                reg = reg.SetBit(10, value);
            }
        }

        public bool NameTableY
        {
            get
            {
                return reg.TestBit(11);
            }
            set
            {
                reg = reg.SetBit(11, value);
            }
        }

    }
}
