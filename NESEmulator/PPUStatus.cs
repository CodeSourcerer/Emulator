using System;
using NESEmulator.Util;

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

        /// <summary>
        /// Sprite overflow
        /// </summary>
        public bool SpriteOverflow
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

        public bool SpriteZeroHit
        {
            get
            {
                return reg.TestBit(6);
            }
            set
            {
                reg = reg.SetBit(6, value);
            }
        }

        /// <summary>
        /// Vertical blank started
        /// </summary>
        public bool VerticalBlank
        {
            get
            {
                return reg.TestBit(7);
            }
            set
            {
                reg = reg.SetBit(7, value);
            }
        }
    }
}
