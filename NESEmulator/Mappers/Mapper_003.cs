using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace NESEmulator.Mappers
{
    public class Mapper_003 : Mapper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Mapper_003));

        private byte _chrBank;    // currently selected CHR bank
        private uint _pCHRBank;   // Offset into CHR ROM

        public override bool HasBusConflicts { get => true; }

        public override bool PRGRAMEnable { get => false; protected set => _ = value; }

        public Mapper_003(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x8000)
                return false;

            if (addr <= 0xFFFF)
            {
                mapped_addr = (uint)(addr & ((nPRGBanks > 1) ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x8000)
                return false;

            if (addr <= 0xFFFF)
            {
                _chrBank = data;
                if (nCHRBanks < 5) _chrBank &= 0x03;
                updateCHRBankOffsets();
                Log.Debug($"Switch CHR bank to {_chrBank}");

                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x2000)
            {
                mapped_addr = (uint)(_pCHRBank + (addr & 0x1FFF));
                return true;
            }

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
            _chrBank = 0;
            updateCHRBankOffsets();
        }

        private void updateCHRBankOffsets()
        {
            _pCHRBank = (uint)(_chrBank * 0x2000);
        }
    }
}
