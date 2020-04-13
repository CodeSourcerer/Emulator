using System;

namespace NESEmulator.APU
{
    /// <summary>
    /// This is just a countdown timer that will execute a callback when it reaches 0.
    /// </summary>
    public class APUDivider
    {
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

        public APUDivider(EventHandler callback)
        {
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
