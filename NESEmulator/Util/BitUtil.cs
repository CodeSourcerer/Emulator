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
            destByte |= (byte)((bitValue ? 1 : 0) << bitToSet);
            return destByte;
        }

        public static byte SetBit(this byte destByte, int bitToSet, int bitValue)
        {
            return destByte.SetBit(bitToSet, bitValue != 0);
        }

        public static bool TestBit(this ushort input, int bitToTest)
        {
            return (ushort)((input >> bitToTest) & 0x0001) == 1;
        }

        public static ushort SetBit(this ushort input, int bitToSet, bool bitValue)
        {
            input |= (ushort)((bitValue ? 1 : 0) << bitToSet);
            return input;
        }

        public static ushort SetBit(this ushort input, int bitToSet, int bitValue)
        {
            return input.SetBit(bitToSet, bitValue != 0);
        }
    }
}
