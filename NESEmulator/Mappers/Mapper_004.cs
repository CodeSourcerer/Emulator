using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using NESEmulator.Util;

namespace NESEmulator.Mappers
{
    public class Mapper_004 : Mapper, IScanlineCounterMapper
    {
        private static new readonly ILog Log = LogManager.GetLogger(typeof(Mapper_004));

        public override bool HasBusConflicts { get => false; }

        public override bool PRGRAMEnable { get; protected set; }

        public override bool HasScanlineCounter { get => true; }

        private byte _irqReload;
        private byte _irqCounter;
        private bool _irqEnable;
        private byte _bankSelect;
        private int _targetRegister { get => _bankSelect & 0x07; }
        private byte[] _register = new byte[8]; // 0-5 = CHR, 6-7 = PRG
        private uint[] _pPRGBank = new uint[4];
        private uint[] _pCHRBank = new uint[8];

        private int PRGROMBankMode
        {
            get => (_bankSelect & 0x40) >> 6;
        }

        private int CHRInversion
        {
            get => (_bankSelect & 0x80) >> 7;
        }

        public Mapper_004(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x6000)
                return false;

            if (addr >= 0x6000 && addr < 0x8000)
            {
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            if (addr < 0xA000)
            {
                mapped_addr = (uint)(_pPRGBank[0] + (addr & 0x1FFF));
                //Log.Debug($"Read from bank 0 [mapped_addr={mapped_addr:X4}]");
                return true;
            }

            if (addr < 0xC000)
            {
                mapped_addr = (uint)(_pPRGBank[1] + (addr & 0x1FFF));
                //Log.Debug($"Read from bank 1 [mapped_addr={mapped_addr:X4}]");
                return true;
            }

            if (addr < 0xE000)
            {
                mapped_addr = (uint)(_pPRGBank[2] + (addr & 0x1FFF));
                //Log.Debug($"Read from bank 2 [mapped_addr={mapped_addr:X4}]");
                return true;
            }

            if (addr <= 0xFFFF)
            {
                mapped_addr = (uint)(_pPRGBank[3] + (addr & 0x1FFF));
                //Log.Debug($"Read from bank 3 [mapped_addr={mapped_addr:X4}]");
                return true;
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x6000)
                return false;

            if (addr >= 0x6000 && addr < 0x8000)
            {
                //Log.Debug($"Attempting to write to PRG RAM [addr={addr:X4}]");
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            // Even addresses between 0x8000 and 0x9FFF set bank select register
            if (addr < 0xA000 && (addr & 1) == 0)
            {
                _bankSelect = (byte)(data & 0xE7);
                //Log.Debug($"[addr={addr:X4}] BankSelect={_bankSelect:X2} [PRGROMBankMode={PRGROMBankMode}] [CHRInversion={CHRInversion}] [TargetRegister={_targetRegister}]");
                return true;
            }

            // Odd addresses between 0x8000 and 0x9FFF set new bank value
            if (addr < 0xA000 && (addr & 1) == 1)
            {
                _register[_targetRegister] = data;
                if (_targetRegister == 0 || _targetRegister == 1)
                    _register[_targetRegister] &= 0xFE;

                //Log.Debug($"Register[{_targetRegister}]={data:X2}");
                updateCHRBankOffsets();
                updatePRGBankOffsets();
                return true;
            }

            // Even addresses between 0xA000 and 0xC000 set cartridge mirror mode (if not using 4 screen)
            if (addr < 0xC000 && (addr & 1) == 0)
            {
                if (!cartridge.UseFourScreen)
                {
                    Log.Debug($"Write: [MirrorMode={data & 1}]");
                    if ((data & 1) == 0)
                        cartridge.mirror = Cartridge.Mirror.VERTICAL;
                    else
                        cartridge.mirror = Cartridge.Mirror.HORIZONTAL;
                }

                return true;
            }

            // Odd addresses between 0xA000 and 0xC000 set PRG RAM write protection
            if (addr < 0xC000 && (addr & 1) == 1)
            {
                PRGRAMEnable = data.TestBit(7);
                Log.Debug($"Write: [RAMEnable={PRGRAMEnable}]");
                return true;
            }

            // Even addresses between 0xC000 and 0xE000 set the IRQ counter reload value
            if (addr < 0xE000 && (addr & 1) == 0)
            {
                _irqReload = data;
                //Log.Debug($"Write: [IRQReload={_irqReload}]");
                return true;
            }

            // Odd addresses between 0xC000 and 0xE000 sets IRQ reload flag
            if (addr < 0xE000 && (addr & 1) == 1)
            {
                _irqCounter = 0;
                //Log.Debug("Write: Set IRQReloadFlag");
                return true;
            }

            if (addr <= 0xFFFF && (addr & 1) == 0)
            {
                _irqEnable = false;
                //Log.Debug($"IRQEnable={_irqEnable}");
                //_irqActive = false;
                return true;
            }

            if (addr <= 0xFFFF && (addr & 1) == 1)
            {
                _irqEnable = true;
                //Log.Debug($"IRQEnable={_irqEnable}");
                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x0400)
            {
                mapped_addr = (uint)(_pCHRBank[0] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x0800)
            {
                mapped_addr = (uint)(_pCHRBank[1] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x0C00)
            {
                mapped_addr = (uint)(_pCHRBank[2] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x1000)
            {
                mapped_addr = (uint)(_pCHRBank[3] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x1400)
            {
                mapped_addr = (uint)(_pCHRBank[4] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x1800)
            {
                mapped_addr = (uint)(_pCHRBank[5] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x1C00)
            {
                mapped_addr = (uint)(_pCHRBank[6] + (addr & 0x03FF));
                return true;
            }

            if (addr < 0x2000)
            {
                mapped_addr = (uint)(_pCHRBank[7] + (addr & 0x03FF));
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
            Log.Debug("Mapper 004 reset");
            _bankSelect = 0;
            _irqCounter = 0;
            _irqReload = 0;
            _irqEnable = false;

            updatePRGBankOffsets();
            updateCHRBankOffsets();
        }

        private void updatePRGBankOffsets()
        {
            _pPRGBank[1] = (uint)((_register[7] & 0x3F) * 0x2000);
            _pPRGBank[3] = (uint)((nPRGBanks * 2 - 1) * 0x2000);

            switch (PRGROMBankMode)
            {
                case 0:
                    _pPRGBank[0] = (uint)((_register[6] & 0x3F) * 0x2000);
                    _pPRGBank[2] = (uint)((nPRGBanks * 2 - 2) * 0x2000);
                    break;
                case 1:
                    _pPRGBank[0] = (uint)((nPRGBanks * 2 - 2) * 0x2000);
                    _pPRGBank[2] = (uint)((_register[6] & 0x3F) * 0x2000);
                    break;
            }
        }

        private void updateCHRBankOffsets()
        {
            switch (CHRInversion)
            {
                case 0:
                    _pCHRBank[0] = (uint)(_register[0] * 0x0400);
                    _pCHRBank[1] = (uint)(_register[0] * 0x0400 + 0x0400);
                    _pCHRBank[2] = (uint)(_register[1] * 0x0400);
                    _pCHRBank[3] = (uint)(_register[1] * 0x0400 + 0x0400);
                    _pCHRBank[4] = (uint)(_register[2] * 0x0400);
                    _pCHRBank[5] = (uint)(_register[3] * 0x0400);
                    _pCHRBank[6] = (uint)(_register[4] * 0x0400);
                    _pCHRBank[7] = (uint)(_register[5] * 0x0400);
                    break;
                case 1:
                    _pCHRBank[0] = (uint)(_register[2] * 0x0400);
                    _pCHRBank[1] = (uint)(_register[3] * 0x0400);
                    _pCHRBank[2] = (uint)(_register[4] * 0x0400);
                    _pCHRBank[3] = (uint)(_register[5] * 0x0400);
                    _pCHRBank[4] = (uint)(_register[0] * 0x0400);
                    _pCHRBank[5] = (uint)(_register[0] * 0x0400 + 0x0400);
                    _pCHRBank[6] = (uint)(_register[1] * 0x0400);
                    _pCHRBank[7] = (uint)(_register[1] * 0x0400 + 0x0400);
                    break;
            }
        }

        public void OnSpriteFetch(object sender, EventArgs e)
        {
            if (_irqCounter == 0)
            {
                _irqCounter = _irqReload;
                cartridge.Mapper_InvokeInterrupt(this, new InterruptEventArgs(InterruptType.CLEAR_IRQ));
                //Log.Debug("IRQCounter reloaded");
            }
            else
            {
                _irqCounter--;
                if (_irqCounter == 0 && _irqEnable)
                {
                    //Log.Debug("Triggering interrupt");
                    cartridge.Mapper_InvokeInterrupt(this, new InterruptEventArgs(InterruptType.IRQ));
                    //_irqEnable = false; // ??
                }
            }
        }
    }
}
