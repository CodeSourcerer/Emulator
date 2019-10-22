using System;
using System.Collections.Generic;
using System.Text;

using NESEmulator.Util;

namespace NESEmulator
{
    public enum SequenceMode { FourStep, FiveStep }

    public class APUFrameCounter
    {
        public bool InterruptInhibit { get; set; }

        public SequenceMode Mode { get; set; }

        private IEnumerable<Channel> _audioChannels;

        public APUFrameCounter(IEnumerable<Channel> audioChannels)
        {
            _audioChannels = audioChannels;
        }

        /// <summary>
        /// Called every CPU clock cycle, which is half of an APU cycle.
        /// </summary>
        /// <param name="apuCycle">APU cycle count</param>
        /// <param name="isHalf">true if half APU cycle</param>
        public void Clock(ulong apuCycle, bool isHalf)
        {

        }
    }
}
