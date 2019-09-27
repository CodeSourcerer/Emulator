using System;
using NESEmulator;

namespace NESEmulator.Mappers
{
    public class Mapper_000 : Mapper
    {
        public Mapper_000(byte prgBanks, byte chrBanks)
            : base(prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr)
        {
            mapped_addr = 0;

            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr)
        {
            mapped_addr = 0;

            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr)
        {
            mapped_addr = 0;

            if (addr <= 0x1FFF)
            {
                mapped_addr = addr;
                return true;
            }

            return false;
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr)
        {
            mapped_addr = 0;

            return false;
        }
    }
}
