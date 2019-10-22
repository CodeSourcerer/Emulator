using System;
using System.Collections.Generic;
using System.Text;

using NESEmulator.Util;

namespace NESEmulator
{
    public struct APUFrameCounter
    {
        public byte reg { get; set; }

        public bool InterruptInhibit
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

        public bool FiveStepSeqMode
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
