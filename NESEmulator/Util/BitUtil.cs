using System;
namespace NESEmulator.Util
{
    public static class BitUtil
    {
        public static bool TestBit(this byte byteToTest, int bitToTest)
        {
            return (byte)((byteToTest >> bitToTest) & 0x01) == 1;
        }

        public static byte SetBit(this byte destByte, int bitToSet, bool bitValue)
        {
            if (bitValue)
                destByte |= (byte)(1 << bitToSet);
            else
                destByte &= (byte)~(1 << bitToSet);

            return destByte;
        }

        public static byte SetBit(this byte destByte, int bitToSet, int bitValue)
        {
            return destByte.SetBit(bitToSet, bitValue != 0);
        }

        public static byte Flip(this byte byteToFlip)
        {
            byteToFlip = (byte)((byteToFlip & 0xF0) >> 4 | (byteToFlip & 0x0F) << 4);
            byteToFlip = (byte)((byteToFlip & 0xCC) >> 2 | (byteToFlip & 0x33) << 2);
            byteToFlip = (byte)((byteToFlip & 0xAA) >> 1 | (byteToFlip & 0x55) << 1);
            return byteToFlip;
        }

        public static bool TestBit(this ushort input, int bitToTest)
        {
            return (ushort)((input >> bitToTest) & 0x0001) == 1;
        }

        public static ushort SetBit(this ushort input, int bitToSet, bool bitValue)
        {
            if (bitValue)
                input |= (ushort)(1 << bitToSet);
            else
                input &= (ushort)~(1 << bitToSet);

            return input;
        }

        public static ushort SetBit(this ushort input, int bitToSet, int bitValue)
        {
            return input.SetBit(bitToSet, bitValue != 0);
        }
    }
}
