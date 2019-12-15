using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.Mappers
{
    public class Mapper_004 : Mapper
    {
        public Mapper_004(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }


        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr >= 0x8000 && addr <= 0x9FFE & addr % 2 == 0)
            {
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            throw new NotImplementedException();
        }

        public override void reset()
        {
            throw new NotImplementedException();
        }
    }
}
