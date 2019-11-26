using System;

namespace NESEmulator
{
    public class PulseChannel : Channel
    {
        public int ChannelNum { get; private set; }

        public PulseChannel(int channel)
        {
            this.ChannelNum = channel;
        }

        public void Clock(ulong clockCycles)
        {
            throw new NotImplementedException();
        }

        public void ClockHalfFrame()
        {
            throw new NotImplementedException();
        }

        public void ClockQuarterFrame()
        {
            throw new NotImplementedException();
        }

        public byte Read(ushort addr)
        {
            throw new NotImplementedException();
        }

        public void Write(ushort addr, byte data)
        {
            
        }
    }
}
