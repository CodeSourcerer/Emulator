using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public struct PPULoopyRegister
    {
        public ushort reg;

        public byte CoarseX
        {
            get
            {
                return (byte)(reg & 0x1F);
            }
            set
            {
                reg = reg.SetBit(0, value.TestBit(0));
                reg = reg.SetBit(1, value.TestBit(1));
                reg = reg.SetBit(2, value.TestBit(2));
                reg = reg.SetBit(3, value.TestBit(3));
                reg = reg.SetBit(4, value.TestBit(4));
            }
        }

        public byte CoarseY
        {
            get
            {
                return (byte)((reg >> 5) & 0x1F);
            }
            set
            {
                reg = reg.SetBit(5, value.TestBit(0));
                reg = reg.SetBit(6, value.TestBit(1));
                reg = reg.SetBit(7, value.TestBit(2));
                reg = reg.SetBit(8, value.TestBit(3));
                reg = reg.SetBit(9, value.TestBit(4));
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

        public byte FineY
        {
            get
            {
                return (byte)((reg >> 12) & 0x07);
            }
            set
            {
                reg = reg.SetBit(12, value.TestBit(0));
                reg = reg.SetBit(13, value.TestBit(1));
                reg = reg.SetBit(14, value.TestBit(2));
            }
        }

        public bool Unused
        {
            get
            {
                return reg.TestBit(15);
            }
            set
            {
                reg = reg.SetBit(15, value);
            }
        }
    }
}
