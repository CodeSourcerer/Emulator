using System;
using System.Collections.Generic;
using System.Text;

using NESEmulator.Util;

namespace NESEmulator
{
    public enum SequenceMode { FourStep, FiveStep }
    public enum SequenceAction { QuarterFrame, HalfFrame, Interrupt }

    public class APUFrameCounter
    {
        public bool InterruptInhibit { get; set; }

        public SequenceMode Mode { get; set; }

        private const ulong STEP1 = 7457;
        private const ulong STEP2 = 14913;
        private const ulong STEP3 = 22371;
        private const ulong FOURSTEP_STEP4 = 29828;
        private const ulong FIVESTEP_STEP4 = 29829;

        private Dictionary<ulong, SequenceAction[]> _fourStepSequence;
        private Dictionary<ulong, Action> _fiveStepSequence;
        private IEnumerable<Channel> _audioChannels;
        private ulong _clockCounter;
        private CS2A03 _apu;

        public APUFrameCounter(IEnumerable<Channel> audioChannels, CS2A03 connectedAPU)
        {
            this._audioChannels = audioChannels;
            this._apu = connectedAPU;
            buildSequencers();
        }

        /// <summary>
        /// Called every CPU clock cycle, which is half of an APU cycle.
        /// </summary>
        /// <param name="cpuCycle">CPU cycle count</param>
        public void Clock(ulong cpuCycle)
        {
            _clockCounter++;

            foreach(var channel in _audioChannels)
            {
            }
        }

        private void buildSequencers()
        {
            this._fourStepSequence = new Dictionary<ulong, SequenceAction[]>(10);
            this._fourStepSequence.Add(STEP1, new SequenceAction[] { SequenceAction.QuarterFrame });
            this._fiveStepSequence = new Dictionary<ulong, Action>(10);
        }
    }
}
