using System;
using System.Collections.Generic;
using System.Text;

using NESEmulator.Util;

namespace NESEmulator
{
    public struct APUStatus
    {
        public byte reg { get; set; }

        public byte channel
        {
            get
            {
                return (byte)(reg & 0x03);
            }
            set
            {
                reg = (byte)(((reg >> 2) << 2) | (value & 0x03));
            }
        }

        public bool TriangleEnable
        {
            get
            {
                return reg.TestBit(2);
            }
            set
            {
                reg.SetBit(2, value);
            }
        }

        public bool NoiseEnable
        {
            get
            {
                return reg.TestBit(3);
            }
            set
            {
                reg.SetBit(3, value);
            }
        }

        public bool DMCEnable
        {
            get
            {
                return reg.TestBit(4);
            }
            set
            {
                reg.SetBit(4, value);
            }
        }

        public bool FrameInterruptEnable
        {
            get
            {
                return reg.TestBit(6);
            }
            set
            {
                reg.SetBit(6, value);
            }
        }
        public bool DMCInterruptEnable
        {
            get
            {
                return reg.TestBit(7);
            }
            set
            {
                reg.SetBit(7, value);
            }
        }
    }
}
