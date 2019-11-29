using System;
using System.Collections.Generic;

namespace NESEmulator
{
    /// <summary>
    /// Length counter (aka sequencer) for audio channels
    /// </summary>
    public class APULengthCounter
    {
        public bool Halt { get; set; }

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;

                if (!_enabled)
                    _linearLength = 0;
            }
        }

        private ushort _timerReload;
        public ushort TimerReload
        {
            get => _timerReload;
            set
            {
                if (Enabled)
                    _timerReload = value;
            }
        }

        private ushort _timer;
        public ushort Timer
        {
            get => _timer;
            set
            {
                if (Enabled)
                    _timer = value;
            }
        }

        private ushort _linearLength;
        public ushort LinearLength
        {
            get => _linearLength;
            set
            {
                if (Enabled)
                    _linearLength = value;
            }
        }

        public event EventHandler CounterElapsed;

        private static Dictionary<byte, byte> _lengthTable; // { get; set; }

        static APULengthCounter()
        {
            buildLengthTable();
        }

        public APULengthCounter()
        {
        }

        public void Clock()
        {
            if (!Halt)
            {
                this.Timer--;

                if (this.Timer == 0xFFFF)
                {
                    this.CounterElapsed?.Invoke(this, EventArgs.Empty);
                    this.Timer = this.TimerReload;
                }
            }
        }

        public void LoadLength(byte tableEntry)
        {
            if (tableEntry < 0x20)
                LinearLength = _lengthTable[tableEntry];
        }

        public void ClearLength()
        {
            LinearLength = 0;
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
