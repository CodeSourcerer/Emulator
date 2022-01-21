using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public interface IInterruptingDevice
    {
        bool IRQActive { get; }
    }
}
