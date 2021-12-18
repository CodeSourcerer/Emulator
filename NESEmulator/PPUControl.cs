using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public struct PPUControl
    {
        public byte reg;

        public bool NameTableX
        {
            get => reg.TestBit(0);
            set => reg = reg.SetBit(0, value);
        }

        public bool NameTableY
        {
            get => reg.TestBit(1);
            set => reg = reg.SetBit(1, value);
        }

        public bool IncrementMode
        {
            get => reg.TestBit(2);
            set => reg = reg.SetBit(2, value);
        }

        /// <summary>
        /// Indicates which Sprite Pattern table to use (0x0000 : false, 0x1000 : true)
        /// </summary>
        public bool PatternSprite
        {
            get => reg.TestBit(3);
            set => reg = reg.SetBit(3, value);
        }

        /// <summary>
        /// Indicates which Background Pattern table to use (0x0000 : false, 0x1000 : true)
        /// </summary>
        public bool PatternBackground
        {
            get => reg.TestBit(4);
            set => reg = reg.SetBit(4, value);
        }

        /// <summary>
        /// Indicates 8x8 or 8x16 sprite. 8x8 : false, 8x16 : true
        /// </summary>
        public bool SpriteSize
        {
            get => reg.TestBit(5);
            set => reg = reg.SetBit(5, value);
        }

        public bool SlaveMode
        {
            get => reg.TestBit(6);
            set => reg = reg.SetBit(6, value);
        }

        public bool EnableNMI
        {
            get => reg.TestBit(7);
            set => reg = reg.SetBit(7, value);
        }

        public static explicit operator PPUControl(byte v)
        {
            return new PPUControl()
            {
                reg = v
            };
        }
    }
}
