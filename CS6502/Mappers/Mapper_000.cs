using System;
using CS6502;

namespace CS6502.Mappers
{
    public class Mapper_000 : Mapper
    {
        public Mapper_000(byte prgBanks, byte chrBanks)
            : base(prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool cpuMapWrite(ushort addr, uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapRead(ushort addr, uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapWrite(ushort addr, uint mapped_addr)
        {
            throw new NotImplementedException();
        }
    }
}
