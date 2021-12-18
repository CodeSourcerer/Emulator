using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public enum CPUInstructionType
    {
        Read,
        Write,
        R_W,
        R_M_W,
        Branch,
        Special
    }
}
