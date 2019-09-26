using System;
namespace CS6502
{
    /// <summary>
    /// Represents the RAM connected to the NES bus
    /// </summary>
    public class Ram : BusDevice
    {
        private byte[] _ram;
        private readonly ushort _ramAmount;
        private readonly ushort _maxAddressable;

        public Ram(ushort ramAmount, ushort maxAddressable)
        {
            _maxAddressable = maxAddressable;
            _ramAmount = ramAmount;
            _ram = new byte[_ramAmount + 1];
        }

        public bool Read(ushort addr, out byte data)
        {
            if (addr > _maxAddressable)
            {
                data = 0;
                return false;
            }

            data = _ram[addr & _ramAmount];

            return true;
        }

        public bool Write(ushort addr, byte data)
        {
            if (addr > _maxAddressable)
                return false;

            _ram[addr & _ramAmount] = data;

            return true;
        }
    }
}
