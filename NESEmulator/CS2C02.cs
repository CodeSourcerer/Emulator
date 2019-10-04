using System;
using csPixelGameEngine;

namespace NESEmulator
{
    /// <summary>
    /// This represents the NES' Picture Processing Unit (PPU) 2C02
    /// </summary>
    public class CS2C02 : BusDevice
    {
        private const ushort ADDR_PALETTE = 0x3F00;

        public BusDeviceType DeviceType { get { return BusDeviceType.PPU; } }

        // PPU has it's own bus
        private Bus _ppuBus;

        private PPUStatus _status;
        private PPUMask _mask;
        private PPUControl _control;
        private PPULoopyRegister _vram_addr;
        private PPULoopyRegister _tram_addr;

        // Internal communications
        private byte _addressLatch;
        private byte _ppuDataBuffer;

        private Sprite _screen = new Sprite(256, 240);

        // Pixel "dot" position information
        private short  _scanline;
        private ushort _cycle;

        // Background rendering
        private byte _bg_nextTileId;
        private byte _bg_nextTileAttrib;
        private byte _bg_nextTileLSB;
        private byte _bg_nextTileMSB;
        private ushort _bg_shifterPatternLo;
        private ushort _bg_shifterPatternHi;
        private ushort _bg_shifterAttribLo;
        private ushort _bg_shifterAttribHi;

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

        /// <summary>
        /// This function draw the CHR ROM for a given pattern table into
        /// an olc::Sprite, using a specified palette. Pattern tables consist
        /// of 16x16 "tiles or characters". It is independent of the running
        /// emulation and using it does not change the systems state, though
        /// it gets all the data it needs from the live system. Consequently,
        /// if the game has not yet established palettes or mapped to relevant
        /// CHR ROM banks, the sprite may look empty. This approach permits a 
        /// "live" extraction of the pattern table exactly how the NES, and 
        /// ultimately the player would see it.
        /// 
        /// A tile consists of 8x8 pixels. On the NES, pixels are 2 bits, which
        /// gives an index into 4 different colours of a specific palette. There
        /// are 8 palettes to choose from. Colour "0" in each palette is effectively
        /// considered transparent, as those locations in memory "mirror" the global
        /// background colour being used. This mechanics of this are shown in 
        /// detail in ppuRead() & ppuWrite()
        /// 
        /// Characters on NES
        /// ~~~~~~~~~~~~~~~~~
        /// The NES stores characters using 2-bit pixels. These are not stored sequentially
        /// but in singular bit planes. For example:
        ///
        /// 2-Bit Pixels       LSB Bit Plane     MSB Bit Plane
        /// 0 0 0 0 0 0 0 0	  0 0 0 0 0 0 0 0   0 0 0 0 0 0 0 0
        /// 0 1 1 0 0 1 1 0	  0 1 1 0 0 1 1 0   0 0 0 0 0 0 0 0
        /// 0 1 2 0 0 2 1 0	  0 1 1 0 0 1 1 0   0 0 1 0 0 1 0 0
        /// 0 0 0 0 0 0 0 0 =  0 0 0 0 0 0 0 0 + 0 0 0 0 0 0 0 0
        /// 0 1 1 0 0 1 1 0	  0 1 1 0 0 1 1 0   0 0 0 0 0 0 0 0
        /// 0 0 1 1 1 1 0 0	  0 0 1 1 1 1 0 0   0 0 0 0 0 0 0 0
        /// 0 0 0 2 2 0 0 0	  0 0 0 1 1 0 0 0   0 0 0 1 1 0 0 0
        /// 0 0 0 0 0 0 0 0	  0 0 0 0 0 0 0 0   0 0 0 0 0 0 0 0
        ///
        /// The planes are stored as 8 bytes of LSB, followed by 8 bytes of MSB
        /// </summary>
        /// <param name="i"></param>
        /// <param name="palette"></param>
        /// <returns></returns>
        public Sprite GetPatternTable(int i, byte palette)
        {
            // Loop through all the 16x16 tiles
            for (int tileY = 0; tileY < 16; tileY++)
            {
                for (int tileX = 0; tileX < 16; tileX++)
                {
                    // Convert the 2D tile coordinate into an offset into the pattern
                    // table memory.
                    int offset = tileY * 256 + tileX * 16;

                    // Loop through 8x8 character/sprite at tile
                    for (int row = 0; row < 8; row++)
                    {
                        // For each row, we need to read both bit planes of the character
                        // in order to extract the least significant and most significant 
                        // bits of the 2 bit pixel value. in the CHR ROM, each character
                        // is stored as 64 bits of lsb, followed by 64 bits of msb. This
                        // conveniently means that two corresponding rows are always 8
                        // bytes apart in memory.
                        byte tileLSB = ppuRead((ushort)(i * 0x1000 + offset + row + 0x0000));
                        byte tileMSB = ppuRead((ushort)(i * 0x1000 + offset + row + 0x0008));

                        // Now we have a single row of the two bit planes for the character
                        // we need to iterate through the 8-bit words, combining them to give
                        // us the final pixel index
                        for (int col = 0; col < 8; col++)
                        {
                            // We can get the index value by simply adding the bits together
                            // but we're only interested in the lsb of the row words because...
                            byte pixel = (byte)((tileLSB & 0x01) + (tileMSB & 0x01));

                            // ...we will shift the row words 1 bit right for each column of
                            // the character.
                            tileLSB >>= 1;
                            tileMSB >>= 1;

                            // Now we know the location and NES pixel value for a specific location
                            // in the pattern table, we can translate that to a screen color, and an
                            // (x,y) location in the sprite
                            _patternTable[i].SetPixel(
                                (uint)(tileX * 8 + (7 - col)),  // Because we are using the lsb of the row word first
                                                                // we are effectively reading the row from right
                                                                // to left, so we need to draw the row "backwards"
                                (uint)(tileY * 8 + row),
                                GetColorFromPaletteRam(palette, pixel)
                            );
                        }
                    }
                }
            }
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

            // These are the live PPU registers that repsond
            // to being read from in various ways. Note that not
            // all the registers are capable of being read from
            // so they just return 0x00
            switch (addr)
            {
                case 0x0000:    // Control
                    dataRead = true;
                    break;
                case 0x0001:    // Mask
                    dataRead = true;
                    break;
                case 0x0002:    // Status
                    // Reading from the status register has the effect of resetting different parts of the circuit. 
                    // Only the top three bits contain status information, however it is possible that some "noise"
                    // gets picked up on the bottom 5 bits which represent the last PPU bus transaction. Some games
                    // "may" use this noise as valid data (even though they probably shouldn't)
                    data = (byte)((_status.reg & 0xE0) | (_ppuDataBuffer & 0x1F));

                    // Clear the vertical blanking flag
                    _status.VerticalBlank = false;

                    // Reset Loopy's Address latch flag
                    _addressLatch = 0;

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

        // TODO: I think this should be a read from _ppuBus... will investigate later
        private byte ppuRead(ushort addr, bool rdonly = false)
        {
            byte data = 0;
            addr &= 0x3FFF;

            // Do stuff...

            return data;
        }

        // TODO: I think this should be a write to the _ppuBus... will investigate later
        private void ppuWrite(ushort addr, byte data)
        {

        }

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

        public Pixel GetColorFromPaletteRam(byte palette, byte pixel)
        {
            // This is a convenience function that takes a specified palette and pixel
            // index and returns the appropriate screen colour.
            // "0x3F00"       - Offset into PPU addressable range where palettes are stored
            // "palette << 2" - Each palette is 4 bytes in size
            // "pixel"        - Each pixel index is either 0, 1, 2 or 3
            // "& 0x3F"       - Stops us reading beyond the bounds of the palScreen array
            return _palScreen[ppuRead((ushort)(ADDR_PALETTE + (palette << 2) + pixel)) & 0x3F];

            // Note: We dont access tblPalette directly here, instead we know that ppuRead()
            // will map the address onto the seperate small RAM attached to the PPU bus.
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
