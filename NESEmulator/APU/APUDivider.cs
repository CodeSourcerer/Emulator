using System;
using log4net;

namespace NESEmulator.APU
{
    /// <summary>
    /// This is just a countdown timer that will execute a callback when it reaches 0.
    /// </summary>
    public class APUDivider
    {
        private readonly ILog Log = LogManager.GetLogger(typeof(APUDivider));

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
        private readonly string _unitName;

        //public event EventHandler DividerReachedZero;
        private EventHandler _dividerReachedZero;

        public APUDivider(EventHandler callback, string unitName = "")
        {
            _dividerReachedZero = callback;
            _unitName = unitName;
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
