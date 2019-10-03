using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public struct PPUControl
    {
        public byte reg;

        public bool NameTableX
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

        public bool NameTableY
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

        public bool IncrementMode
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

        public bool PatternSprite
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

        public bool PatternBackground
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

        public bool SpriteSize
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

        public bool SlaveMode
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

        public bool EnableNMI
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
