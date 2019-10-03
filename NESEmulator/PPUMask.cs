using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public struct PPUMask
    {
        public byte reg;

        public bool GrayScale
        {
            get
            {
                return reg.TestBit(0);
            }
            set
            {
                reg = reg.SetBit(0, value);
            }
        }

        public bool RenderBackgroundLeft
        {
            get
            {
                return reg.TestBit(1);
            }
            set
            {
                reg = reg.SetBit(1, value);
            }
        }

        public bool RenderSpritesLeft
        {
            get
            {
                return reg.TestBit(2);
            }
            set
            {
                reg = reg.SetBit(2, value);
            }
        }

        public bool RenderBackground
        {
            get
            {
                return reg.TestBit(3);
            }
            set
            {
                reg = reg.SetBit(3, value);
            }
        }

        public bool RenderSprites
        {
            get
            {
                return reg.TestBit(4);
            }
            set
            {
                reg = reg.SetBit(4, value);
            }
        }

        public bool EnhanceRed
        {
            get
            {
                return reg.TestBit(5);
            }
            set
            {
                reg = reg.SetBit(5, value);
            }
        }

        public bool EnhanceGreen
        {
            get
            {
                return ((byte)(reg >> 6) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 6);
            }
        }

        public bool EnhanceBlue
        {
            get
            {
                return ((byte)(reg >> 7) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 7);
            }
        }
    }
}
