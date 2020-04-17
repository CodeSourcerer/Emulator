using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NESEmulator.Util;

namespace NESEmulator.APU
{
    public enum SequenceMode { FourStep, FiveStep }
    public enum SequenceAction { QuarterFrame, HalfFrame, Interrupt }

    public class APUFrameCounter
    {
        public bool InterruptInhibit { get; set; }

        public bool FrameInterrupt;

        public SequenceMode Mode { get; set; }

        private const ulong STEP1 = 7457;
        private const ulong STEP2 = 14913;
        private const ulong STEP3 = 22371;
        private const ulong FOURSTEP_STEP4 = 29828;
        private const ulong FOURSTEP_FINAL = 29830;
        private const ulong FIVESTEP_STEP4 = 29829;
        private const ulong FIVESTEP_STEP5 = 37281;
        private const ulong FIVESTEP_FINAL = 37282;

        private Dictionary<ulong, SequenceAction[]> _fourStepSequence;
        private Dictionary<ulong, SequenceAction[]> _fiveStepSequence;
        private IEnumerable<Channel> _audioChannels;
        private ulong _clockCounter;
        private CS2A03 _apu;

        public APUFrameCounter(IEnumerable<Channel> audioChannels, CS2A03 connectedAPU)
        {
            _audioChannels = audioChannels;
            _apu = connectedAPU;
            buildSequencers();
        }

        /// <summary>
        /// Called every CPU clock cycle, which is half of an APU cycle.
        /// </summary>
        /// <param name="cpuCycle">CPU cycle count</param>
        public void Clock(ulong cpuCycle)
        {
            _clockCounter++;

            SequenceAction[] sequenceActions = null;
            if (this.Mode == SequenceMode.FourStep)
            {
                if (_fourStepSequence.ContainsKey(_clockCounter))
                {
                    sequenceActions = _fourStepSequence[_clockCounter];
                }
            }
            else
            {
                if (_fiveStepSequence.ContainsKey(_clockCounter))
                {
                    sequenceActions = _fiveStepSequence[_clockCounter];
                }
            }

            if (sequenceActions != null)
            {
                //Console.WriteLine("FrameCounter perform sequences at clockCount: {0}", _clockCounter);
                foreach (var channel in _audioChannels)
                {
                    foreach (var action in sequenceActions)
                    {
                        switch (action)
                        {
                            case SequenceAction.QuarterFrame:
                                channel.ClockQuarterFrame();
                                break;
                            case SequenceAction.HalfFrame:
                                channel.ClockHalfFrame();
                                break;
                            case SequenceAction.Interrupt:
                                if (!this.InterruptInhibit && !this.FrameInterrupt)
                                    this.FrameInterrupt = true;
                                break;
                        }
                    }
                }
            }

            if (this.Mode == SequenceMode.FourStep && _clockCounter == FOURSTEP_FINAL)
                _clockCounter = 0;
            else if (this.Mode == SequenceMode.FiveStep && _clockCounter == FIVESTEP_FINAL)
                _clockCounter = 0;
        }

        public bool IsInterruptCycle()
        {
            if (this.Mode == SequenceMode.FiveStep)
                return false;   // Interrupts cannot happen in this mode

            if (this.InterruptInhibit)
                return false;   // Interrupts are inhibited

            Dictionary<ulong, SequenceAction[]> sequences = _fourStepSequence;

            return _clockCounter == 0 || sequences.ContainsKey(_clockCounter) &&
                   sequences[_clockCounter].Any(seq => seq == SequenceAction.Interrupt);
        }

        public void Reset()
        {
            _clockCounter = 0; // not quite, but whatever
        }

        private void buildSequencers()
        {
            _fourStepSequence = new Dictionary<ulong, SequenceAction[]>(10);
            _fourStepSequence.Add(STEP1, new SequenceAction[] { SequenceAction.QuarterFrame });
            _fourStepSequence.Add(STEP2, new SequenceAction[] { SequenceAction.QuarterFrame, SequenceAction.HalfFrame });
            _fourStepSequence.Add(STEP3, new SequenceAction[] { SequenceAction.QuarterFrame });
            _fourStepSequence.Add(FOURSTEP_STEP4, new SequenceAction[] { SequenceAction.Interrupt });
            _fourStepSequence.Add(FOURSTEP_STEP4 + 1, new SequenceAction[] { SequenceAction.QuarterFrame, SequenceAction.HalfFrame, SequenceAction.Interrupt });
            _fourStepSequence.Add(FOURSTEP_FINAL, new SequenceAction[] { SequenceAction.Interrupt });
            
            _fiveStepSequence = new Dictionary<ulong, SequenceAction[]>(10);
            _fiveStepSequence.Add(STEP1, new SequenceAction[] { SequenceAction.QuarterFrame });
            _fiveStepSequence.Add(STEP2, new SequenceAction[] { SequenceAction.QuarterFrame, SequenceAction.HalfFrame });
            _fiveStepSequence.Add(STEP3, new SequenceAction[] { SequenceAction.QuarterFrame });
            // step 4, do nothing??
            _fiveStepSequence.Add(FIVESTEP_STEP5, new SequenceAction[] { SequenceAction.QuarterFrame, SequenceAction.HalfFrame });
        }
    }
}
