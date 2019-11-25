using System;
namespace NESEmulator
{
    public class TriangleChannel : Channel
    {
        private const ushort ADDR_LINEARCOUNTER          = 0x4008;
        private const ushort ADDR_TIMERLOW               = 0x400A;
        private const ushort ADDR_TIMERHI_LENCOUNTERLOAD = 0x400B;

        private ushort _timer;
        private byte _lengthCounter;
        private byte _linearCounter;
        private bool _linearCounterControl;

        public TriangleChannel()
        {
        }

        /// <summary>
        /// Called by frame sequencer every half frame
        /// </summary>
        public void ClockHalfFrame()
        {
            // Triangle channel ignores half frames
        }

        /// <summary>
        /// Called by frame sequencer every quarter frame
        /// </summary>
        public void ClockQuarterFrame()
        {
            //Console.WriteLine("Triangle channel quarter frame");
        }

        public void Clock(ulong clockCyles)
        {

        }

        public byte Read(ushort addr)
        {
            return 0;
        }

        public void Write(ushort addr, byte data)
        {
            switch (addr)
            {
                case ADDR_LINEARCOUNTER:
                case ADDR_TIMERLOW:
                case ADDR_TIMERHI_LENCOUNTERLOAD:
                    Console.WriteLine("Triangle Channel Write: addr: {0:X2}; data: {1:X2}", addr, data);
                    break;
            }
        }
    }
}
