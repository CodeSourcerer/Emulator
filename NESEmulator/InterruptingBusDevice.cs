using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public delegate void InterruptingDeviceHandler(object sender, InterruptEventArgs e);

    public interface InterruptingBusDevice : BusDevice
    {
        event InterruptingDeviceHandler RaiseInterrupt;
    }
}
