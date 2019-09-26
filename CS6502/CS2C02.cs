using System;
using csPixelGameEngine;

namespace CS6502
{
    /// <summary>
    /// This represents the NES' Picture Processing Unit (PPU) 2C02
    /// </summary>
    public class CS2C02 : BusDevice
    {
        public BusDeviceType DeviceType { get { return BusDeviceType.PPU; } }

        // PPU has it's own bus
        private Bus _ppuBus;

        private Pixel[] _palScreen = new Pixel[0x40];
        private Sprite _screen = new Sprite(256, 240);
        private Sprite[] _nameTable = { new Sprite(256, 240), new Sprite(256, 240) };
        private Sprite[] _patternTable = { new Sprite(128, 128), new Sprite(128, 128) };

        private ushort _scanline;
        private ushort _cycle;
        private byte[][] _tblName = new byte[2][];
        //private byte[][] _tblPattern = new byte[2][];
        private byte[] _palette = new byte[32];

        private Cartridge _cartridge;

        public CS2C02()
        {
            // Create PPU bus with its devices...

            _tblName[0] = new byte[1024];
            _tblName[1] = new byte[1024];
            //_tblPattern[0] = new byte[4096];
            //_tblPattern[1] = new byte[4096];
        }

        #region Debugging utilities

        public Sprite GetScreen()
        {
            return _screen;
        }

        public Sprite GetNameTable(int i)
        {
            if (i < 2)
                return _nameTable[i];

            return null;
        }

        public Sprite GetPatternTable(int i)
        {
            if (i < 2)
                return _patternTable[i];

            return null;
        }

        public bool FrameComplete { get; set; }

        #endregion // Debugging utilities

        #region Bus Communications

        public bool Read(ushort addr, out byte data)
        {
            bool dataRead = false;
            data = 0;

            switch (addr)
            {
                case 0x0000:    // Control
                    dataRead = true;
                    break;
                case 0x0001:    // Mask
                    dataRead = true;
                    break;
                case 0x0002:    // Status
                    dataRead = true;
                    break;
                case 0x0003:    // OAM Address
                    dataRead = true;
                    break;
                case 0x0004:    // OAM Data
                    dataRead = true;
                    break;
                case 0x0005:    // Scroll
                    dataRead = true;
                    break;
                case 0x0006:    // PPU Address
                    dataRead = true;
                    break;
                case 0x0007:    // PPU Data
                    dataRead = true;
                    break;
            }

            return dataRead;
        }

        public bool Write(ushort addr, byte data)
        {
            bool dataWritten = false;

            switch (addr)
            {
                case 0x0000:    // Control
                    dataWritten = true;
                    break;
                case 0x0001:    // Mask
                    dataWritten = true;
                    break;
                case 0x0002:    // Status
                    dataWritten = true;
                    break;
                case 0x0003:    // OAM Address
                    dataWritten = true;
                    break;
                case 0x0004:    // OAM Data
                    dataWritten = true;
                    break;
                case 0x0005:    // Scroll
                    dataWritten = true;
                    break;
                case 0x0006:    // PPU Address
                    dataWritten = true;
                    break;
                case 0x0007:    // PPU Data
                    dataWritten = true;
                    break;
            }

            return dataWritten;
        }

        public void Reset()
        { }

        #endregion // Bus Communications

        public void ConnectCartridge(Cartridge cartridge)
        {
            _cartridge = cartridge;
        }

        public void clock()
        {

        }
    }
}
