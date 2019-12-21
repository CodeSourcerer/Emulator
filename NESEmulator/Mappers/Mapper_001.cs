using System;
using System.Collections.Generic;
using System.Text;
using log4net;
using NESEmulator.Util;

namespace NESEmulator.Mappers
{
    public class Mapper_001 : Mapper
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(Mapper_001));

        private byte   _shiftRegister;
        private byte   _controlRegister;
        private byte[] _chrBank = new byte[2];  // currently selected CHR banks
        private byte   _prgBank;                // currently selected PRG bank
        private bool   _prgRAMEnable;
        private uint[] _pPRGBank = new uint[2]; // Offsets into PRG ROM
        private uint[] _pCHRBank = new uint[2]; // Offsets into CHR ROM/RAM
        private ulong  _lastClockCycleWrite;

        private int PRGROMBankMode
        {
            get => (_controlRegister & 0x0C) >> 2;
        }

        private int CHRROMBankMode
        {
            get => (_controlRegister & 0x10) >> 4;
        }

        public Mapper_001(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
            _shiftRegister = 0x10;
            _controlRegister = 0x0C;
            _prgBank = 0;
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x6000)
                return false;

            if (addr < 0x8000)
            {
                // Read from RAM on cartridge
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            if (addr < 0xC000)
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

            if (_lastClockCycleWrite == cartridge.ThisClockCycle)
                return false;

            if (addr >= 0x6000 && addr < 0x8000)
            {
                // Write to RAM on cartridge
                //Log.Debug($"Writing to cartridge RAM");
                mapped_addr = 0xFFFFFFFF;
                _lastClockCycleWrite = cartridge.ThisClockCycle;
                return true;
            }
            else if (addr >= 0x8000)
            {
                if (data.TestBit(7) == true)
                {
                    _shiftRegister = 0x10;
                    _controlRegister |= 0x0C;
                    updatePRGBankOffsets();
                }
                else
                {
                    if ((_shiftRegister & 0x01) == 1)
                    {
                        _shiftRegister >>= 1;
                        _shiftRegister |= (byte)((data & 0x01) << 4);
                        loadRegister(addr);
                        _shiftRegister = 0x10;
                    }
                    else
                    {
                        _shiftRegister >>= 1;
                        _shiftRegister |= (byte)((data & 0x01) << 4);
                    }
                }
                _lastClockCycleWrite = cartridge.ThisClockCycle;
                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x2000)
            {
                int bankNum = addr < 0x1000 ? 0 : 1;
                mapped_addr = (uint)(_pCHRBank[bankNum] + (addr & 0x0FFF));

               // Log.Debug($"CHR ROM read [bankNum={bankNum}] [addr={addr:X4}]");
                return true;
            }

            return false;
        }

        public override bool ppuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            // Treat like RAM
            if (addr < 0x2000)
            {
                //ushort bankSize = (ushort)(_chrROMBankMode == 0 ? 0x1FFF : 0x0FFF);

                //if (_chrROMBankMode == 0)
                //{
                //    // Use full 8KB window
                //    mapped_addr = (uint)(_pCHRBank[0] + (addr & bankSize));
                //    return true;
                //}

                int bankNum = addr < 0x1000 ? 0 : 1;
                mapped_addr = (uint)(_pCHRBank[bankNum] + (addr & 0x0FFF));

                Log.Debug($"CHR ROM write [bankNum={bankNum}] [addr={addr:X4}]");
                return true;
            }

            return false;
        }

        public override void reset()
        {
            Log.Debug($"Mapper 001 cartridge reset");
            _shiftRegister = 0x10;
            _controlRegister = 0x0C;
            updatePRGBankOffsets();
            updateCHRBankOffsets();
        }

        /// <summary>
        /// This deals with either setting up the cartridge or setting CHR/ROM banks after
        /// loading the shift register.
        /// </summary>
        /// <param name="addr">Address used when shift register finished loading.</param>
        private void loadRegister(ushort addr)
        {
            if (addr >= 0x8000 && addr < 0xA000)
            {
                // Control 
                _controlRegister = _shiftRegister;
                int mirroring = _controlRegister & 0x03;
                switch (mirroring)
                {
                    case 0:
                        cartridge.mirror = Cartridge.Mirror.ONESCREEN_LO;
                        break;
                    case 1:
                        cartridge.mirror = Cartridge.Mirror.ONESCREEN_HI;
                        break;
                    case 2:
                        cartridge.mirror = Cartridge.Mirror.VERTICAL;
                        break;
                    case 3:
                        cartridge.mirror = Cartridge.Mirror.HORIZONTAL;
                        break;
                }

                // PRG ROM bank mode (0, 1: switch 32 KB at $8000, ignoring low bit of bank number;
                //                    2: fix first bank at $8000 and switch 16 KB bank at $C000;
                //                    3: fix last bank at $C000 and switch 16 KB bank at $8000)
                // CHR ROM bank mode(0: switch 8 KB at a time; 1: switch two separate 4 KB banks)
                Log.Debug($"Control register written. [Mirroring={mirroring}] [prgROMBankMode={PRGROMBankMode}] [chrROMBankMode={CHRROMBankMode}]");
            }
            else if (addr < 0xC000)
            {
                // Select CHR bank 0
                _chrBank[0] = (byte)(CHRROMBankMode == 0 ? (_shiftRegister & 0x1E) : (_shiftRegister & 0x1F));
                updateCHRBankOffsets();
                Log.Debug($"CHR bank 0 written. [chrBank0={_shiftRegister}]");
            }
            else if (addr < 0xE000)
            {
                // Select CHR bank 1
                _chrBank[1] = (byte)(_shiftRegister & 0x1F);
                updateCHRBankOffsets();
                Log.Debug($"CHR bank 1 written. [chrBank1={_shiftRegister}]");
            }
            else
            {
                _prgBank = (byte)(_shiftRegister & 0x0F);
                updatePRGBankOffsets();

                _prgRAMEnable = _shiftRegister.TestBit(4);
                Log.Debug($"PRG bank written. [prgBank={_prgBank}]");
            }
        }

        private bool isPRG32KB() => (_controlRegister & 0x08) == 0;

        private void updatePRGBankOffsets()
        {
            switch (PRGROMBankMode)
            {
                case 0:
                case 1:
                    // 32KB bank switching
                    _pPRGBank[0] = (uint)((_prgBank & 0x0E) * 0x4000);
                    _pPRGBank[1] = (uint)(_pPRGBank[0] + 0x4000);
                    break;
                case 2:
                    _pPRGBank[0] = 0;                                   // fixed to first bank
                    _pPRGBank[1] = (uint)(_prgBank * 0x4000);
                    break;
                case 3:
                    _pPRGBank[0] = (uint)(_prgBank * 0x4000);
                    _pPRGBank[1] = (uint)((nPRGBanks - 1) * 0x4000);    // fixed to last bank
                    break;
            }
        }

        private void updateCHRBankOffsets()
        {
            switch (CHRROMBankMode)
            {
                case 0:
                    _pCHRBank[0] = (uint)(_chrBank[0] * 0x1000);
                    _pCHRBank[1] = (uint)(_pCHRBank[0] + 0x1000);
                    break;
                case 1:
                    _pCHRBank[0] = (uint)(_chrBank[0] * 0x1000);
                    _pCHRBank[1] = (uint)(_chrBank[1] * 0x1000);
                    break;
            }
        }
    }
}
