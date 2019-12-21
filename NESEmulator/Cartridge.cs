using System;
using System.IO;

using NESEmulator.Util;
using NESEmulator.Mappers;
using log4net;

namespace NESEmulator
{
    public class Cartridge : BusDevice
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Cartridge));

        public enum Mirror
        {
            HORIZONTAL,
            VERTICAL,
            ONESCREEN_LO,
            ONESCREEN_HI
        };

        public BusDeviceType DeviceType { get { return BusDeviceType.CART; } }
        private Mirror _mirror;
        public Mirror mirror
        {
            get => _mirror;
            set 
            {
                _mirror = value;
                Log.Debug($"Mirroring mode set to {_mirror}");
            }
        }

        public bool ImageValid { get; private set; }

        private byte     mapperId;
        private byte     nPRGBanks;
        private byte     nCHRBanks;
        protected byte[] PRGRAM;
        protected byte[] PRGROM;
        protected byte[] CHRMemory;

        private Mapper mapper;

        public ulong ThisClockCycle { get; private set; }

        public Cartridge()
        {
            mirror = Mirror.HORIZONTAL;

            PRGROM = new byte[32 * 1024];
            PRGRAM = new byte[32 * 1024];
        }

        #region Bus Interface
        /// <summary>
        /// Attempt to read byte of data from cartridge. Will only read if address
        /// is readable from loaded cartridge
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        /// <returns>true if byte read, false if address outside of range</returns>
        public bool Read(ushort addr, out byte data)
        {
            data = 0;

            uint mapped_addr;
            if (mapper.cpuMapRead(addr, out mapped_addr, ref data))
            {
                if (mapped_addr != 0xFFFFFFFF)
                {
                    data = PRGROM[mapped_addr];
                }
                else
                {
                    // Data came from cartridge RAM
                    data = PRGRAM[addr & 0x1FFF];
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempt to write byte of data to cartridge. Will only write if address
        /// is writable to loaded cartridge
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        /// <returns>true if byte written, false of address outside of range</returns>
        public bool Write(ushort addr, byte data)
        {
            uint mapped_addr;
            if (mapper.cpuMapWrite(addr, out mapped_addr, ref data))
            {
                if (mapped_addr == 0xFFFFFFFF)
                {
                    // Write data to cartridge RAM
                    PRGRAM[addr & 0x1FFF] = data;
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Perform cartridge reset.
        /// </summary>
        public void Reset()
        {
            if (mapper != null)
                mapper.reset();
        }

        /// <summary>
        /// Attempt to read data from cartridge from PPU bus.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        /// <returns>true if byte read, false if address outside of range</returns>
        public bool ppuRead(ushort addr, out byte data)
        {
            data = 0;

            uint mapped_addr;
            if (mapper.ppuMapRead(addr, out mapped_addr, ref data))
            {
                if (mapped_addr != 0xFFFFFFFF)
                {
                    data = CHRMemory[mapped_addr];
                }
                else
                {
                    throw new NotImplementedException("This shouldn't happen");
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Attempt to write data to cartridge from PPU bus.
        /// </summary>
        /// <param name="addr"></param>
        /// <param name="data"></param>
        /// <returns>true if byte written, false of address outside of range</returns>
        public bool ppuWrite(ushort addr, byte data)
        {
            uint mapped_addr;
            if (mapper.ppuMapWrite(addr, out mapped_addr, ref data))
            {
                if (mapped_addr != 0xFFFFFFFF)
                {
                    CHRMemory[mapped_addr] = data;
                }
                else
                {
                    throw new NotImplementedException("This shouldn't happen");
                }
                return true;
            }

            return false;
        }

        public void Clock(ulong clockCounter)
        {
            ThisClockCycle = clockCounter;
        }
        #endregion // Bus Interface

        /// <summary>
        /// Read ROM/cartridge data
        /// </summary>
        /// <param name="cartStream">BinaryReader stream with ROM data</param>
        /// <remarks>
        /// Portable library does not support File operations, so that must be
        /// done outside of this lib.
        /// </remarks>
        public void ReadCartridge(BinaryReader cartStream)
        {
            CartridgeHeader cartridgeHeader = new CartridgeHeader();

            cartridgeHeader.name            = cartStream.ReadChars(4);
            cartridgeHeader.prg_rom_chunks  = cartStream.ReadByte();    // byte 4
            cartridgeHeader.chr_rom_chunks  = cartStream.ReadByte();    // byte 5
            cartridgeHeader.mapper1         = cartStream.ReadByte();    // byte 6
            cartridgeHeader.mapper2         = cartStream.ReadByte();    // byte 7
            cartridgeHeader.prg_ram_size    = cartStream.ReadByte();    // byte 8
            cartridgeHeader.chr_ram_size    = cartStream.ReadByte();    // byte 9
            cartridgeHeader.tv_system1      = cartStream.ReadByte();    // byte 10
            cartridgeHeader.unused          = cartStream.ReadChars(5);  // byte 11-16

            switch (cartridgeHeader.tv_system1)
            {
                case 0:
                case 2:
                    break;
                default:
                    throw new NotSupportedException("Non-NTSC ROM not supported");
            }

            // If a "trainer" exists we need to read past it
            // before we get to the good stuff
            if ((cartridgeHeader.mapper1 & 0x04) != 0)
            {
                byte[] stuff = new byte[512];
                cartStream.Read(stuff, 0, 512);
            }

            // Determine mapper ID
            mapperId = (byte)(((cartridgeHeader.mapper2 >> 4) << 4) | (cartridgeHeader.mapper1 >> 4));
            mirror = cartridgeHeader.mapper1.TestBit(0) ? Mirror.VERTICAL : Mirror.HORIZONTAL;

            bool is_iNesV2 = (cartridgeHeader.mapper2 & 0x0C) == 8;
            if (is_iNesV2)
                Log.Debug("iNES 2.0 ROM detected");

            // "Discover" file format
            byte fileType = 1;

            if (fileType == 0)
            {
                // TODO: implement later
            }
            else if (fileType == 1)
            {
                Log.Debug($"Reading {(cartridgeHeader.prg_rom_chunks * 16384) / 1024} KB of PRG data from ROM");
                nPRGBanks = cartridgeHeader.prg_rom_chunks;
                PRGROM = new byte[nPRGBanks * 16384];
                cartStream.Read(PRGROM, 0, PRGROM.Length);

                if (cartridgeHeader.chr_rom_chunks == 0)
                    cartridgeHeader.chr_rom_chunks = 1;
                Log.Debug($"Reading {(cartridgeHeader.chr_rom_chunks * 8192) / 1024} KB of CHR data from ROM");
                nCHRBanks = cartridgeHeader.chr_rom_chunks;
                CHRMemory = new byte[nCHRBanks * 8192];
                cartStream.Read(CHRMemory, 0, CHRMemory.Length);
            }
            else if (fileType == 2)
            {
                // TODO: implement later
            }

            // Load appropriate mapper
            switch (mapperId)
            {
                case 0:
                    mapper = new Mapper_000(this, nPRGBanks, nCHRBanks);
                    break;
                case 1:
                    mapper = new Mapper_001(this, nPRGBanks, nCHRBanks);
                    break;
            }

            if (mapper != null)
                ImageValid = true;
        }
    }
}
