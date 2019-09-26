using System;
namespace CS6502
{
    public abstract class Mapper
    {
        public Mapper(byte prgBanks, byte chrBanks)
        {
            nPRGBanks = prgBanks;
            nCHRBanks = chrBanks;
        }

        // Transform CPU bus address into PRG ROM offset
        public abstract bool cpuMapRead(ushort addr, uint mapped_addr);
        public abstract bool cpuMapWrite(ushort addr, uint mapped_addr);

        // Transform PPU bus address into CHR ROM offset
        public abstract bool ppuMapRead(ushort addr, uint mapped_addr);
        public abstract bool ppuMapWrite(ushort addr, uint mapped_addr);

        protected byte nPRGBanks;
        protected byte nCHRBanks;
    }
}
