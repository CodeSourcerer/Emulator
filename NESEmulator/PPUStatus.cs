using System;
namespace NESEmulator
{
    public struct PPUStatus
    {
        public byte reg;

        /// <summary>
        /// No one should want this, but here it is
        /// </summary>
        public byte Unused
        {
            get
            {
                return (byte)(reg >> 3);
            }
        }

        /// <summary>
        /// Sprite overflow
        /// </summary>
        public bool SpriteOverflow
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

        public bool SpriteZeroHit
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

        /// <summary>
        /// Vertical blank started
        /// </summary>
        public bool VerticalBlank
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
