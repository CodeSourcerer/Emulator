using System;

namespace NESEmulator.APU
{
    public class APUDivider
    {
        public enum DividerType { COUNTDOWN, LFSR }

        public DividerType TypeOfDivider { get; set; }

        private int _counterReload;
        public int CounterReload
        {
            get => _counterReload;
            set
            {
                _counterReload = value;
                _counter = _counterReload;
            }
        }

        private int _counter;

        //public event EventHandler DividerReachedZero;
        private EventHandler _dividerReachedZero;

        public APUDivider(DividerType dividerType, EventHandler callback)
        {
            TypeOfDivider = dividerType;
            _dividerReachedZero = callback;
        }

        public void Clock()
        {
            if (_counter == 0)
            {
                _counter = CounterReload;
                //DividerReachedZero?.Invoke(this, EventArgs.Empty);
                _dividerReachedZero(this, EventArgs.Empty);
            }
            else
            {
                --_counter;
            }
        }

        public void Reset()
        {
            _counter = CounterReload;
        }
    }
}
