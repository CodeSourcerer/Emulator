using System;
using csPixelGameEngine;

namespace NESEmulator
{
    /// <summary>
    /// This represents the NES' Picture Processing Unit (PPU) 2C02
    /// </summary>
    public class CS2C02 : BusDevice
    {
        public BusDeviceType DeviceType { get { return BusDeviceType.PPU; } }

        // PPU has it's own bus
        private Bus _ppuBus;

        private PPUStatus _status;
        private PPUMask _mask;
        private PPUControl _control;

        private Sprite _screen = new Sprite(256, 240);

        private short  _scanline;
        private ushort _cycle;
        private byte[][] _tblName = new byte[2][];
        private Sprite[] _nameTable = { new Sprite(256, 240), new Sprite(256, 240) };

        // TODO: This is connected to the PPU's bus, so I think it would be better
        // to make this a BusDevice and attach to _ppuBus.
        private byte[][] _tblPattern = new byte[2][];
        private Sprite[] _patternTable = { new Sprite(128, 128), new Sprite(128, 128) };

        private byte[] _palette = new byte[32];
        private Pixel[] _palScreen = new Pixel[0x40];

        private Cartridge _cartridge;

        private Random _random;

        public CS2C02()
        {
            _random = new Random();

            _status = new PPUStatus();
            _mask = new PPUMask();

            // Create PPU bus with its devices...

            _tblName[0] = new byte[1024];
            _tblName[1] = new byte[1024];
            //_tblPattern[0] = new byte[4096];
            //_tblPattern[1] = new byte[4096];

            buildPalette();
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

        //public Sprite GetPatternTable(int i)
        //{
        //    if (i < 2)
        //        return _patternTable[i];

        //    return null;
        //}

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

        public void Clock()
        {
            // Temporary: fake noise
            _screen.SetPixel((uint)(_cycle - 1), (uint)(_scanline+1), _palScreen[(_random.Next(2) == 1) ? 0x3F : 0x30]);

            _cycle++;

            if (_cycle >= 341)
            {
                _cycle = 0;
                _scanline++;
                if(_scanline >= 261)
                {
                    _scanline = -1;
                    FrameComplete = true;
                }
            }
        }

        private void buildPalette()
        {
            _palScreen[0x00] = new Pixel(84, 84, 84);
            _palScreen[0x01] = new Pixel(0, 30, 116);
            _palScreen[0x02] = new Pixel(8, 16, 144);
            _palScreen[0x03] = new Pixel(48, 0, 136);
            _palScreen[0x04] = new Pixel(68, 0, 100);
            _palScreen[0x05] = new Pixel(92, 0, 48);
            _palScreen[0x06] = new Pixel(84, 4, 0);
            _palScreen[0x07] = new Pixel(60, 24, 0);
            _palScreen[0x08] = new Pixel(32, 42, 0);
            _palScreen[0x09] = new Pixel(8, 58, 0);
            _palScreen[0x0A] = new Pixel(0, 64, 0);
            _palScreen[0x0B] = new Pixel(0, 60, 0);
            _palScreen[0x0C] = new Pixel(0, 50, 60);
            _palScreen[0x0D] = new Pixel(0, 0, 0);
            _palScreen[0x0E] = new Pixel(0, 0, 0);
            _palScreen[0x0F] = new Pixel(0, 0, 0);

            _palScreen[0x10] = new Pixel(152, 150, 152);
            _palScreen[0x11] = new Pixel(8, 76, 196);
            _palScreen[0x12] = new Pixel(48, 50, 236);
            _palScreen[0x13] = new Pixel(92, 30, 228);
            _palScreen[0x14] = new Pixel(136, 20, 176);
            _palScreen[0x15] = new Pixel(160, 20, 100);
            _palScreen[0x16] = new Pixel(152, 34, 32);
            _palScreen[0x17] = new Pixel(120, 60, 0);
            _palScreen[0x18] = new Pixel(84, 90, 0);
            _palScreen[0x19] = new Pixel(40, 114, 0);
            _palScreen[0x1A] = new Pixel(8, 124, 0);
            _palScreen[0x1B] = new Pixel(0, 118, 40);
            _palScreen[0x1C] = new Pixel(0, 102, 120);
            _palScreen[0x1D] = new Pixel(0, 0, 0);
            _palScreen[0x1E] = new Pixel(0, 0, 0);
            _palScreen[0x1F] = new Pixel(0, 0, 0);

            _palScreen[0x20] = new Pixel(236, 238, 236);
            _palScreen[0x21] = new Pixel(76, 154, 236);
            _palScreen[0x22] = new Pixel(120, 124, 236);
            _palScreen[0x23] = new Pixel(176, 98, 236);
            _palScreen[0x24] = new Pixel(228, 84, 236);
            _palScreen[0x25] = new Pixel(236, 88, 180);
            _palScreen[0x26] = new Pixel(236, 106, 100);
            _palScreen[0x27] = new Pixel(212, 136, 32);
            _palScreen[0x28] = new Pixel(160, 170, 0);
            _palScreen[0x29] = new Pixel(116, 196, 0);
            _palScreen[0x2A] = new Pixel(76, 208, 32);
            _palScreen[0x2B] = new Pixel(56, 204, 108);
            _palScreen[0x2C] = new Pixel(56, 180, 204);
            _palScreen[0x2D] = new Pixel(60, 60, 60);
            _palScreen[0x2E] = new Pixel(0, 0, 0);
            _palScreen[0x2F] = new Pixel(0, 0, 0);

            _palScreen[0x30] = new Pixel(236, 238, 236);
            _palScreen[0x31] = new Pixel(168, 204, 236);
            _palScreen[0x32] = new Pixel(188, 188, 236);
            _palScreen[0x33] = new Pixel(212, 178, 236);
            _palScreen[0x34] = new Pixel(236, 174, 236);
            _palScreen[0x35] = new Pixel(236, 174, 212);
            _palScreen[0x36] = new Pixel(236, 180, 176);
            _palScreen[0x37] = new Pixel(228, 196, 144);
            _palScreen[0x38] = new Pixel(204, 210, 120);
            _palScreen[0x39] = new Pixel(180, 222, 120);
            _palScreen[0x3A] = new Pixel(168, 226, 144);
            _palScreen[0x3B] = new Pixel(152, 226, 180);
            _palScreen[0x3C] = new Pixel(160, 214, 228);
            _palScreen[0x3D] = new Pixel(160, 162, 160);
            _palScreen[0x3E] = new Pixel(0, 0, 0);
            _palScreen[0x3F] = new Pixel(0, 0, 0);
        }
    }
}
