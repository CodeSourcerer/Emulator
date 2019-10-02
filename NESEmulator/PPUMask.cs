using System;
namespace NESEmulator
{
    public struct PPUMask
    {
        public byte reg;

        public bool GrayScale
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

        public bool RenderBackgroundLeft
        {
            get
            {
                return ((byte)(reg >> 1) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 1);
            }
        }

        public bool RenderSpritesLeft
        {
            get
            {
                return ((byte)(reg >> 2) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 2);
            }
        }

        public bool RenderBackground
        {
            get
            {
                return ((byte)(reg >> 3) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 3);
            }
        }

        public bool RenderSprites
        {
            get
            {
                return ((byte)(reg >> 4) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 4);
            }
        }

        public bool EnhanceRed
        {
            get
            {
                return ((byte)(reg >> 5) & 0x01) == 1;
            }
            set
            {
                reg |= (byte)((value ? 1 : 0) << 5);
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
