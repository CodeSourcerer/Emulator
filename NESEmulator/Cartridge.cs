using System;
using System.IO;

using NESEmulator.Util;
using NESEmulator.Mappers;

namespace NESEmulator
{
    public class Cartridge : BusDevice
    {
        public enum Mirror
        {
            HORIZONTAL,
            VERTICAL,
            ONESCREEN_LO,
            ONESCREEN_HI
        };

        public BusDeviceType DeviceType { get { return BusDeviceType.CART; } }
        public Mirror mirror { get; set; }

        public bool ImageValid { get; private set; }

        private byte    mapperId;
        private byte    nPRGBanks;
        private byte    nCHRBanks;
        private byte[]  PRGMemory;
        private byte[]  CHRMemory;

        private Mapper mapper;

        public Cartridge()
        {
            mirror = Mirror.HORIZONTAL;

            this.PRGMemory = new byte[32 * 1024];
        }

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
            if (mapper.cpuMapRead(addr, out mapped_addr))
            {
                data = PRGMemory[mapped_addr];
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
            if (mapper.cpuMapWrite(addr, out mapped_addr))
            {
                PRGMemory[mapped_addr] = data;
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
            if (mapper.ppuMapRead(addr, out mapped_addr))
            {
                data = CHRMemory[mapped_addr];
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
            if (mapper.ppuMapWrite(addr, out mapped_addr))
            {
                CHRMemory[mapped_addr] = data;
                return true;
            }

            return false;
        }

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
            cartridgeHeader.prg_rom_chunks  = cartStream.ReadByte();
            cartridgeHeader.chr_rom_chunks  = cartStream.ReadByte();
            cartridgeHeader.mapper1         = cartStream.ReadByte();
            cartridgeHeader.mapper2         = cartStream.ReadByte();
            cartridgeHeader.prg_ram_size    = cartStream.ReadByte();
            cartridgeHeader.chr_ram_size    = cartStream.ReadByte();
            cartridgeHeader.tv_system1      = cartStream.ReadByte();
            cartridgeHeader.unused          = cartStream.ReadChars(5);

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

            // "Discover" file format
            byte fileType = 1;

            if (fileType == 0)
            {
                // TODO: implement later
            }
            else if (fileType == 1)
            {
                nPRGBanks = cartridgeHeader.prg_rom_chunks;
                PRGMemory = new byte[nPRGBanks * 16384];
                cartStream.Read(PRGMemory, 0, PRGMemory.Length);

                nCHRBanks = cartridgeHeader.chr_rom_chunks;
                if (nCHRBanks == 0)
                    CHRMemory = new byte[8192];
                else
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
                    mapper = new Mapper_000(nPRGBanks, nCHRBanks);
                    break;
            }

            if (mapper != null)
                ImageValid = true;
        }

        public void Clock(ulong clockCounter)
        { }
    }
}
