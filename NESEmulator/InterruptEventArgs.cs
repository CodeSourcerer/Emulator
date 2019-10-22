using System;
namespace NESEmulator
{
    public class InterruptEventArgs : EventArgs
    {
        public InterruptType Interrupt { get; set; }

        public InterruptEventArgs(InterruptType interruptType)
            : base()
        {
            this.Interrupt = interruptType;
        }
    }
}
