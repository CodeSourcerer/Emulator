using Microsoft.VisualStudio.TestTools.UnitTesting;
using NESEmulator.Util;

namespace EmulatorTests
{
    [TestClass]
    public class BitUtilTests
    {
        [DataRow((byte)0, 0, (byte)1)]
        [DataRow((byte)0, 1, (byte)2)]
        [DataRow((byte)0, 2, (byte)4)]
        [DataRow((byte)0, 3, (byte)8)]
        [DataRow((byte)0, 4, (byte)16)]
        [DataRow((byte)0, 5, (byte)32)]
        [DataRow((byte)0, 6, (byte)64)]
        [DataRow((byte)0, 7, (byte)128)]
        [DataTestMethod]
        public void SetBit_SetsSingleBit(byte testByte, int testBit, byte expectedResult)
        {
            byte actualByte = testByte.SetBit(testBit, true);

            Assert.AreEqual(expectedResult, actualByte);
        }

        [DataRow((byte)0,   0, false)]
        [DataRow((byte)254, 0, false)]
        [DataRow((byte)1,   0, true)]
        [DataRow((byte)255, 0, true)]
        [DataRow((byte)127, 7, false)]
        [DataRow((byte)128, 7, true)]
        [DataTestMethod]
        public void TestBit_TestsSingleBitInAnyPosition(byte testByte, int bitToTest, bool expectedResult)
        {
            bool actualResult = testByte.TestBit(bitToTest);

            Assert.AreEqual(expectedResult, actualResult);
        }
    }
}
