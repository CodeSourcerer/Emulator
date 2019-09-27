using System;
namespace NESEmulator
{
    public struct CartridgeHeader
    {
        public char[] name;
        public byte prg_rom_chunks;
        public byte chr_rom_chunks;
        public byte mapper1;
        public byte mapper2;
        public byte prg_ram_size;
        public byte tv_system1;
        public byte tv_system2;
        public char[] unused;

        public CartridgeHeader(byte prg_rom_chunks, byte chr_rom_chunks,
            byte mapper1, byte mapper2, byte prg_ram_size, byte tv_system1,
            byte tv_system2, char[] name = null, char[] unused = null)
        {
            if (name != null && name.Length != 4)
                throw new ArgumentException("Must be 4 characters", nameof(name));
            else if (name != null)
                this.name = name;
            else
                this.name = new char[4];

            this.unused = new char[5];

            this.prg_rom_chunks = prg_rom_chunks;
            this.chr_rom_chunks = chr_rom_chunks;
            this.mapper1 = mapper1;
            this.mapper2 = mapper2;
            this.prg_ram_size = prg_ram_size;
            this.tv_system1 = tv_system1;
            this.tv_system2 = tv_system2;
        }
    }
}
