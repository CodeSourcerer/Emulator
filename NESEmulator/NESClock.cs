using System;
using System.Timers;

namespace CS6502
{
    public delegate void ClockTickHandler(object sender, EventArgs e);

    public class NESClock : IDisposable
    {
        private long _systemClockCounter;
        private Timer timer;

        public event ClockTickHandler OnClockTick;

        public NESClock()
        {
            timer = new Timer((1.0d / 60.0d) * 1000.0d);
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        public void Reset()
        {
            _systemClockCounter = 0;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _systemClockCounter++;
            OnClockTick?.Invoke(this, new EventArgs());
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    timer.Elapsed -= Timer_Elapsed;
                    timer.Stop();
                    timer.Dispose();

                    foreach (var handler in OnClockTick.GetInvocationList())
                        OnClockTick -= (ClockTickHandler)handler;
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
