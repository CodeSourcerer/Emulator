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
//        private byte   _prgBankRegister;
        private int    _prgROMBankMode;
        private int    _chrROMBankMode;
        private byte[] _chrBank = new byte[2];  // currently selected CHR banks
        private byte   _prgBank;                // currently selected PRG bank
        private bool   _prgRAMEnable;
        private uint[] _pPRGBank = new uint[2];

        private int PRGROMBankMode
        {
            get => _prgROMBankMode;
            set
            {
                _prgROMBankMode = value;
                switch (_prgROMBankMode)
                {
                    case 0:
                    case 1:
                        _pPRGBank[0] = (uint)(_prgBank * 0x8000);
                        break;
                    case 2:
                        _pPRGBank[0] = 0;   // fixed to first bank
                        _pPRGBank[1] = (uint)(_prgBank * 0x4000);
                        break;
                    case 3:
                        _pPRGBank[0] = (uint)(_prgBank * 0x4000);
                        _pPRGBank[1] = (uint)((nPRGBanks - 1) * 0x4000);    // fixed to last bank
                        break;
                }
            }
        }
        public Mapper_001(Cartridge cartridge, byte prgBanks, byte chrBanks)
            : base(cartridge, prgBanks, chrBanks)
        {
            _shiftRegister = 0x10;
            PRGROMBankMode = 3;
            _prgBank = 0;
        }

        public override bool cpuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr >= 0x6000 && addr < 0x8000)
            {
                // Read from RAM on cartridge
                mapped_addr = 0xFFFFFFFF;
                return true;
            }

            if (isPRG32KB() && addr >= 0x8000)
            {
                //mapped_addr = (uint)((addr % 0x8000) + (_prgBank * 0x8000));
                mapped_addr = _pPRGBank[0] + addr & 0x7FFF;
                return true;
            }

            if (addr >= 0x8000 && addr < 0xC000)
            {
                //int offset = 0;
                //if (_prgROMBankMode == 3 && _prgBank < (nPRGBanks - 1))
                //{
                //    offset = _prgBank * 0x4000;
                //}

                mapped_addr = (uint)(_pPRGBank[0] + (addr & 0x3FFF));

                return true;
            }

            if (addr >= 0xC000)
            {
                //int offset = _prgBank * 0x4000;
                //if (_prgROMBankMode == 3)
                //{
                //    offset = (nPRGBanks - 1) * 0x4000;
                //}

                mapped_addr = (uint)(_pPRGBank[1] + (addr & 0x3FFF));

                return true;
            }

            return false;
        }

        public override bool cpuMapWrite(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr >= 0x6000 && addr < 0x8000)
            {
                // Write to RAM on cartridge
                Log.Debug($"Writing to cartridge RAM");
                mapped_addr = 0xFFFFFFFF;
                return true;
            }
            else if (addr >= 0x8000)
            {
                if (data.TestBit(7) == true)
                    _shiftRegister = 0x10;
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
                        _shiftRegister |= (byte)((data & 0x01) << 3);
                    }
                }
                return true;
            }

            return false;
        }

        public override bool ppuMapRead(ushort addr, out uint mapped_addr, ref byte data)
        {
            mapped_addr = 0;

            if (addr < 0x2000)
            {
                byte chrBank = _chrROMBankMode == 0 ? _chrBank[0] : _chrBank[addr.TestBit(12) ? 1 : 0];
                ushort bankSize = (ushort)(_chrROMBankMode == 0 ? 0x1FFF : 0x0FFF);

                mapped_addr = (uint)((addr & bankSize) + (chrBank * bankSize));

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
                Log.Debug("Writing to CHR ROM");
                byte chrBank = _chrROMBankMode == 0 ? _chrBank[0] : _chrBank[addr.TestBit(12) ? 1 : 0];
                ushort bankSize = (ushort)(_chrROMBankMode == 0 ? 0x1FFF : 0x0FFF);

                mapped_addr = (uint)((addr & bankSize) + (chrBank * bankSize));

                return true;
            }

            return false;
        }

        public override void reset()
        {
            _shiftRegister = 0x10;
            PRGROMBankMode = 3;
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
                int mirroring = _shiftRegister & 0x03;
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
                PRGROMBankMode = (_shiftRegister & 0x0C) >> 2;
                _chrROMBankMode = (_shiftRegister & 0x10) >> 4;
                Log.Debug($"Control register written. [Mirroring={mirroring}] [prgROMBankMode={_prgROMBankMode}] [chrROMBankMode={_chrROMBankMode}]");
            }
            else if (addr >= 0xA000 && addr < 0xC000)
            {
                // Select CHR bank 0
                _chrBank[0] = _shiftRegister;
                Log.Debug($"CHR bank 0 written. [chrBank0={_shiftRegister}]");
            }
            else if (addr >= 0xC000 && addr < 0xE000)
            {
                // Select CHR bank 1
                _chrBank[1] = _shiftRegister;
                Log.Debug($"CHR bank 1 written. [chrBank1={_shiftRegister}]");
            }
            else if (addr >= 0xE000)
            {
                _prgBank = (byte)(_shiftRegister & 0x0F);
                if (isPRG32KB())
                    _prgBank >>= 1; // ignore low bit in 32 KB mode
                if (_prgROMBankMode != 2)
                    _pPRGBank[0] = (uint)(_prgBank * (isPRG32KB() ? 0x8000 : 0x4000));
                if (_prgROMBankMode == 2)
                    _pPRGBank[1] = (uint)(_prgBank * 0x4000);

                _prgRAMEnable = _shiftRegister.TestBit(4);
                Log.Debug($"PRG bank written. [prgBank={_prgBank}]");
            }
        }

        private bool isPRG32KB() => _prgROMBankMode == 0 || _prgROMBankMode == 1;
    }
}
