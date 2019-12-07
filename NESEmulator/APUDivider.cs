using System;

namespace NESEmulator
{
    public class APUDivider
    {
        public enum DividerType { COUNTDOWN, LFSR }

        public DividerType TypeOfDivider { get; set; }

        public int CounterReload { get; set; }

        private int _counter;

        public event EventHandler DividerReachedZero;

        public APUDivider(DividerType dividerType)
        {
            this.TypeOfDivider = dividerType;
        }

        public void Clock()
        {
            if (_counter == 0)
            {
                _counter = CounterReload;
                DividerReachedZero?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                --_counter;
            }
        }
    }
}
