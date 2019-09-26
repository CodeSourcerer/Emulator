using System;
namespace CS6502
{
    public abstract class Mapper
    {
        protected byte nPRGBanks;
        protected byte nCHRBanks;

        public Mapper(byte prgBanks, byte chrBanks)
        {
            nPRGBanks = prgBanks;
            nCHRBanks = chrBanks;
        }

        // Transform CPU bus address into PRG ROM offset
        public abstract bool cpuMapRead(ushort addr, out uint mapped_addr);
        public abstract bool cpuMapWrite(ushort addr, out uint mapped_addr);

        // Transform PPU bus address into CHR ROM offset
        public abstract bool ppuMapRead(ushort addr, out uint mapped_addr);
        public abstract bool ppuMapWrite(ushort addr, out uint mapped_addr);
    }
}
