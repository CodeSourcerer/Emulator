using System;

namespace CS6502
{
    public class Bus : IBus
    {
        private CS6502 cpu;
        private byte[] ram;

        public Bus(CS6502 cpu)
        {
            this.cpu = cpu;
            this.ram = new byte[64 * 1024];
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

        private bool isAddressable(ushort addr)
        {
            return addr >= 0x0000 && addr <= 0xFFFF;
        }
    }
}
