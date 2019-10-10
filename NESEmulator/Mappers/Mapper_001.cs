using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.Mappers
{
    public class Mapper_001 : Mapper
    {
        public Mapper_001(byte prgBanks, byte chrBanks)
            : base(prgBanks, chrBanks)
        {

        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr)
        {
            throw new NotImplementedException();
        }

        public override void reset()
        {
            throw new NotImplementedException();
        }
    }
}
