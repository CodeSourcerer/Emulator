using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NESEmulator
{
    public delegate byte OpCode();
    public delegate byte AddressingMode();

    /// <summary>
    /// Used to compile and store opcodes in the opcode translation table.
    /// </summary>
    struct Instruction
    {
        /// <summary>
        /// Instruction pneumonic
        /// </summary>
        public string name;

        /// <summary>
        /// OpCode function
        /// </summary>
        /// <returns></returns>
        public OpCode operation;

        /// <summary>
        /// OpCode address mode
        /// </summary>
        /// <returns></returns>
        public AddressingMode addr_mode;

        /// <summary>
        /// Cycle count for instruction
        /// </summary>
        public byte cycles;
    }
}
