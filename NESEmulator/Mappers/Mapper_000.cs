﻿using System;
using NESEmulator;

namespace NESEmulator.Mappers
{
    public class Mapper_000 : Mapper
    {
        public override bool HasBusConflicts { get => false; }

        public override bool PRGRAMEnable { get => true; protected set => _ = value; }

        public Mapper_000(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr >= 0x6000 & addr < 0x8000)
            {
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
                return true;
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr >= 0x6000 & addr < 0x8000)
            {
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            if (addr >= 0x8000 && addr <= 0xFFFF)
            {
                mapped_addr = (uint)(addr & (nPRGBanks > 1 ? 0x7FFF : 0x3FFF));
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
            
        }

        public override void PowerOn()
        {
            
        }
    }
}
