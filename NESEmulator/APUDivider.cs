using System;

namespace NESEmulator
{
    public class APUDivider
    {
        public enum DividerType { COUNTDOWN, LFSR }

        public DividerType TypeOfDivider { get; set; }

        public int CounterReload { get; set; }

        private int _counter;

        public APUDivider(DividerType dividerType)
        {
            this.TypeOfDivider = dividerType;
        }

        public void Clock(ulong cycleCount)
        {

        }
    }
}
