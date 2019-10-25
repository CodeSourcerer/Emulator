using System;
namespace NESEmulator
{
    public class TriangleChannel : Channel
    {
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
    }
}
