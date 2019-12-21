using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace NESEmulator.Mappers
{
    public class Mapper_003 : Mapper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Mapper_003));

        public Mapper_003(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            return false;
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            return false;
        }

        public override void reset()
        {
            Log.Debug($"Mapper 003 cartridge reset");
        }
    }
}
