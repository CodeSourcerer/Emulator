using System;

namespace CS6502
{
    public class Bus : IBus
    {
        // private CS6502 cpu;
        private byte[] ram;

        private readonly ushort ramSize;

        public Bus(ushort ramAmount)
        {
            // this.cpu = cpu;
            this.ramSize = ramAmount;
            this.ram = new byte[this.ramSize+1];
        }

        public byte Read(ushort addr, bool readOnly = false)
        {
            if (isAddressable(addr))
            {
                return ram[addr];
            }

            return 0x00;
        }

        public void Write(ushort addr, byte data)
        {
            if (isAddressable(addr))
            {
                ram[addr] = data;
            }
        }

        public void LoadROM(byte[] ROM, ushort offset = 0)
        {
            Array.Copy(ROM, 0, ram, 0, ROM.Length);
            ram[0xFFFC] = (byte)(offset & 0x00FF);
            ram[0xFFFD] = (byte)(offset >> 8);
        }

        private bool isAddressable(ushort addr)
        {
            return addr >= 0x0000 && addr <= ramSize;
        }
    }
}
