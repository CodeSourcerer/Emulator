using System;
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

        public Mirror mirror { get; set; }

        public bool ImageValid { get; private set; }

        private byte mapperId;
        private byte nPRGBanks;
        private byte nCHRBanks;
        private byte[] PRGMemory;
        private byte[] CHRMemory;

        private Mapper mapper;

        public Cartridge(string fileName)
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

        public bool ppuRead(ushort addr, out byte data)
        {
            data = 0;

            return false;
        }

        public bool ppuWrite(ushort addr, byte data)
        {
            return false;
        }
    }
}
