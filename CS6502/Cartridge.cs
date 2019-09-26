using System;
using System.IO;

using CS6502.Mappers;

namespace CS6502
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

        public bool Read(ushort addr, out byte data)
        {
            data = 0;

            // TODO: Use mapper
            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                data = PRGMemory[addr - 0x8000];
                return true;
            }

            return false;
        }

        public bool Write(ushort addr, byte data)
        {
            // TODO: Use mapper
            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                PRGMemory[addr - 0x8000] = data;
                return true;
            }

            return false;
        }

        public void Reset()
        { }

        public bool ppuRead(ushort addr, out byte data)
        {
            data = 0;

            return false;
        }

        public bool ppuWrite(ushort addr, byte data)
        {
            return false;
        }

        public void ReadCartridge(BinaryReader cartStream)
        {
            CartridgeHeader cartridgeHeader = new CartridgeHeader();

            cartridgeHeader.name = cartStream.ReadChars(4);
            cartridgeHeader.prg_rom_chunks = cartStream.ReadByte();
            cartridgeHeader.chr_rom_chunks = cartStream.ReadByte();
            cartridgeHeader.mapper1 = cartStream.ReadByte();
            cartridgeHeader.mapper2 = cartStream.ReadByte();
            cartridgeHeader.prg_ram_size = cartStream.ReadByte();
            cartridgeHeader.tv_system1 = cartStream.ReadByte();
            cartridgeHeader.tv_system2 = cartStream.ReadByte();
            cartridgeHeader.unused = cartStream.ReadChars(5);

            // If a "trainer" exists we need to read past it
            // before we get to the good stuff
            if ((cartridgeHeader.mapper1 & 0x04) != 0)
            {
                byte[] stuff = new byte[512];
                cartStream.Read(stuff, 0, 512);
            }

            // Determine mapper ID
            mapperId = (byte)(((cartridgeHeader.mapper2 >> 4) << 4) | (cartridgeHeader.mapper1 >> 4));
            mirror = (cartridgeHeader.mapper1 & 01) != 0 ? Mirror.VERTICAL : Mirror.HORIZONTAL;

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
    }
}
