using System;
using System.Collections.Generic;
using log4net;

namespace NESEmulator.APU
{
    /// <summary>
    /// Length counter for audio channels
    /// </summary>
    public class APULengthCounter
    {
        private static ILog Log = LogManager.GetLogger(typeof(APULengthCounter));

        public bool Halt { get; set; }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;

                if (!_enabled)
                    Length = 0;
            }
        }

        public ushort Length { get; set; }

        private static Dictionary<byte, byte> _lengthTable; // { get; set; }

        static APULengthCounter()
        {
            buildLengthTable();
        }

        private NesClockEventHandler _callback;
        public APULengthCounter(NesClockEventHandler counterElapsedCallback)
        {
            _callback = counterElapsedCallback;
        }

        public void Clock(ulong clockCycle)
        {
            if (!Halt)
            {
                if (Length == 0)
                {
                    //_callback(this, EventArgs.Empty);
                    //this.CounterElapsed?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    --Length;
                    if (Length == 0)
                        _callback(this, new NesClockEventArgs(clockCycle));
                }
            }
        }

        public void LoadLength(byte tableEntry)
        {
            if (tableEntry < 0x20)
                Length = _lengthTable[tableEntry];
        }

        public void ClearLength()
        {
            Length = 0;
        }

        private static void buildLengthTable()
        {
            _lengthTable = new Dictionary<byte, byte>
            {
                { 0x00, 10  },
                { 0x01, 254 },
                { 0x02, 20  },
                { 0x03, 2   },
                { 0x04, 40  },
                { 0x05, 4   },
                { 0x06, 80  },
                { 0x07, 6   },
                { 0x08, 160 },
                { 0x09, 8   },
                { 0x0A, 60  },
                { 0x0B, 10  },
                { 0x0C, 14  },
                { 0x0D, 12  },
                { 0x0E, 26  },
                { 0x0F, 14  },
                { 0x10, 12  },
                { 0x11, 16  },
                { 0x12, 24  },
                { 0x13, 18  },
                { 0x14, 48  },
                { 0x15, 20  },
                { 0x16, 96  },
                { 0x17, 22  },
                { 0x18, 192 },
                { 0x19, 24  },
                { 0x1A, 72  },
                { 0x1B, 26  },
                { 0x1C, 16  },
                { 0x1D, 28  },
                { 0x1E, 32  },
                { 0x1F, 30  }
            };
        }
    }
}
