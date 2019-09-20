using System;
using System.Collections.Generic;

namespace CS6502
{
    [Flags]
    public enum FLAGS6502
    {
        C = (1 << 0),   // Carry
        Z = (1 << 1),   // Zero
        I = (1 << 2),   // Interrupt enable
        D = (1 << 3),   // Decimal mode (unused right now)
        B = (1 << 4),   // Break
        U = (1 << 5),   // Unused
        V = (1 << 6),   // Overflow
        N = (1 << 7)    // Negative
    }

    public class CS6502
    {
        private IBus bus;

        public CS6502(IBus bus)
        {
            this.bus = bus;
        }

        #region Register Properties
        public byte a { get; set; }
        public byte x { get; set; }
        public byte y { get; set; }
        public byte sp { get; set; }
        public ushort pc { get; set; }
        public FLAGS6502 status { get; set; }
        #endregion // Register Properties

        #region Private attributes
        private byte cycles;
        #endregion // Private attributes

        /// <summary>
        /// Reset CPU to known state
        /// </summary>
        public void Reset()
        {

        }

        /// <summary>
        /// Interrupt Request
        /// </summary>
        public void IRQ()
        {

        }

        /// <summary>
        /// Non-maskable Interrupt Request
        /// </summary>
        public void NMI()
        {

        }

        /// <summary>
        /// Perform clock cycle
        /// </summary>
        public void Clock()
        {

        }

        /// <summary>
        /// Indicates if current instruction has completed (for stepping through code)
        /// </summary>
        /// <returns></returns>
        public bool isComplete()
        {
            return this.cycles == 0;
        }

        /// <summary>
        /// Produces a map of strings, with keys equivalent to instruction start
        /// locations in memory.
        /// </summary>
        /// <param name="start"></param>
        /// <param name="stop"></param>
        /// <returns></returns>
        public Dictionary<ushort, string> Disassemble(ushort start, ushort stop)
        {
            throw new NotImplementedException();
        }

        #region Flag Methods

        private byte getFlag(FLAGS6502 f)
        {
            return this.status.HasFlag(f) ? (byte)1 : (byte)0;
        }

        private void setFlag(FLAGS6502 f, bool v)
        {
            if (v)
            {
                status |= f;
            }
            else
            {
                status &= ~f;
            }
        }

        #endregion // Flag Methods

        #region Bus Methods
        #endregion // Bus Methods

        #region Addressing Modes
        /*****
         * The 6502 can address between 0x0000 - 0xFFFF. The high byte is often referred
         * to as the "page", and the low byte is the offset into that page. This implies
         * there are 256 pages, each containing 256 bytes.
         * Several addressing modes have the potential to require an additional clock
         * cycle if they cross a page boundary. This is combined with several instructions
         * that enable this additional clock cycle. So each addressing function returns
         * a flag saying it has potential, as does each instruction. If both instruction
         * and address function return 1, then an additional clock cycle is required.
         * ***/

        private byte IMP()
        {
            throw new NotImplementedException();
        }

        #endregion // Addressing Modes
    }
}
