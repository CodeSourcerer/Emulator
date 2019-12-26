using System;
using System.Collections.Generic;
using System.Text;
using log4net;

namespace NESEmulator.Mappers
{
    public class Mapper_002 : Mapper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Mapper_002));

        private byte _prgBank;                // currently selected PRG bank
        private uint[] _pPRGBank = new uint[2]; // Offsets into PRG ROM

        public override bool HasBusConflicts { get => false; }

        public override bool PRGRAMEnable { get => true; protected set => _ = value; }

        public Mapper_002(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x8000)
                return false;

            if (addr < 0xC000)  // first switchable bank
            {
                mapped_addr = (uint)(_pPRGBank[0] + (addr & 0x3FFF));
                return true;
            }

            if (addr <= 0xFFFF)
            {
                mapped_addr = (uint)(_pPRGBank[1] + (addr & 0x3FFF));
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
                _prgBank = data;
                updatePRGBankOffsets();

                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr <= 0x1FFF)
            {
                mapped_addr = addr;
                return true;
            }

            return false;
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr <= 0x1FFF)
            {
                mapped_addr = addr;
                return true;
            }

            return false;
        }

        public override void reset()
        {
            Log.Debug($"Mapper 002 cartridge reset");
            _prgBank = 0;
            updatePRGBankOffsets();
        }

        private void updatePRGBankOffsets()
        {
            _pPRGBank[0] = (uint)(_prgBank * 0x4000);
            _pPRGBank[1] = (uint)((nPRGBanks - 1) * 0x4000);    // fixed to last bank
        }
    }
}
