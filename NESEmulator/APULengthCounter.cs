using System;
using System.Collections.Generic;

namespace NESEmulator
{
    public class APULengthCounter
    {
        public bool Halt { get; set; }
        public byte LinearLength { get; private set; }

        public event EventHandler CounterElapsed;

        private Dictionary<byte, byte> _lengthTable; // { get; set; }

        public APULengthCounter()
        {
            buildLengthTable();
        }

        public void Clock()
        {
            if (this.LinearLength > 0 && !Halt)
            {
                this.LinearLength--;

                if (this.LinearLength == 0)
                    this.CounterElapsed?.Invoke(this, EventArgs.Empty);
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

        private void buildLengthTable()
        {
            _lengthTable = new Dictionary<byte, byte>();
            _lengthTable.Add(0x00, 10);
            _lengthTable.Add(0x01, 254);
            _lengthTable.Add(0x02, 20);
            _lengthTable.Add(0x03, 2);
            _lengthTable.Add(0x04, 40);
            _lengthTable.Add(0x05, 4);
            _lengthTable.Add(0x06, 80);
            _lengthTable.Add(0x07, 6);
            _lengthTable.Add(0x08, 160);
            _lengthTable.Add(0x09, 8);
            _lengthTable.Add(0x0A, 60);
            _lengthTable.Add(0x0B, 10);
            _lengthTable.Add(0x0C, 14);
            _lengthTable.Add(0x0D, 12);
            _lengthTable.Add(0x0E, 26);
            _lengthTable.Add(0x0F, 14);
            _lengthTable.Add(0x10, 12);
            _lengthTable.Add(0x11, 16);
            _lengthTable.Add(0x12, 24);
            _lengthTable.Add(0x13, 18);
            _lengthTable.Add(0x14, 48);
            _lengthTable.Add(0x15, 20);
            _lengthTable.Add(0x16, 96);
            _lengthTable.Add(0x17, 22);
            _lengthTable.Add(0x18, 192);
            _lengthTable.Add(0x19, 24);
            _lengthTable.Add(0x1A, 72);
            _lengthTable.Add(0x1B, 26);
            _lengthTable.Add(0x1C, 16);
            _lengthTable.Add(0x1D, 28);
            _lengthTable.Add(0x1E, 32);
            _lengthTable.Add(0x1F, 30);
        }
    }
}
