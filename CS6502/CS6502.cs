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
        private List<Instruction> opcode_lookup;
        private IBus bus;

        #region Well-Known Addresses

        /// <summary>
        /// Address of bottom of stack
        /// </summary>
        private const ushort ADDR_STACK = 0x0100;

        /// <summary>
        /// Address of program counter
        /// </summary>
        private const ushort ADDR_PC = 0xFFFC;
        
        /// <summary>
        /// Address of code for IRQ
        /// </summary>
        private const ushort ADDR_IRQ = 0xFFFE;

        /// <summary>
        /// Address of code for NMI
        /// </summary>
        private const ushort ADDR_NMI = 0xFFFA;

        #endregion // Well-Known Addresses

        public CS6502(IBus bus)
        {
            this.bus = bus;
            build_lookup();
        }

        #region Register Properties
        public byte a { get; set; }
        public byte x { get; set; }
        public byte y { get; set; }
        public byte sp { get; set; }
        public ushort pc { get; set; }
        public FLAGS6502 status { get; set; }
        #endregion // Register Properties

        /// <summary>
        /// Reset CPU to known state
        /// </summary>
        /// <remarks>
        /// This is hard-wired inside the CPU. The
        /// registers are set to 0x00, the status register is cleared except for unused
        /// bit which remains at 1. An absolute address is read from location 0xFFFC
        /// which contains a second address that the program counter is set to. This 
        /// allows the programmer to jump to a known and programmable location in the
        /// memory to start executing from. Typically the programmer would set the value
        /// at location 0xFFFC at compile time.
        /// </remarks>
        public void Reset()
        {
            // Set PC
            addr_abs = ADDR_PC;
            ushort lo = read(addr_abs);
            ushort hi = read((ushort)(addr_abs + 1));
            pc = (ushort)((hi << 8) | lo);

            // Reset internal registers
            a = x = y = 0;
            sp = 0xFD;
            status = 0x00 | FLAGS6502.U;

            // Clear internal helper variables
            addr_rel = addr_abs = 0x0000;
            fetched = 0x00;

            // Reset takes time
            cycles = 8;
        }

        /// <summary>
        /// Interrupt Request
        /// </summary>
        /// <remarks>
        /// // Interrupt requests are a complex operation and only happen if the
        /// "disable interrupt" flag is 0. IRQs can happen at any time, but
        /// you dont want them to be destructive to the operation of the running 
        /// program. Therefore the current instruction is allowed to finish
        /// (which I facilitate by doing the whole thing when cycles == 0) and 
        /// then the current program counter is stored on the stack. Then the
        /// current status register is stored on the stack. When the routine
        /// that services the interrupt has finished, the status register
        /// and program counter can be restored to how they where before it 
        /// occurred. This is impemented by the "RTI" instruction. Once the IRQ
        /// has happened, in a similar way to a reset, a programmable address
        /// is read form hard coded location 0xFFFE, which is subsequently
        /// set to the program counter.
        /// </remarks>
        public void IRQ()
        {
            if (getFlag(FLAGS6502.I) == 0)
            {
                // Push the PC to the stack. It is 16-bits, so requires 2 pushes
                push((byte)((pc >> 8) & 0x00FF));
                push((byte)(pc & 0x00FF));

                // Then push status register to the stack
                setFlag(FLAGS6502.B, false);
                setFlag(FLAGS6502.U, true);
                setFlag(FLAGS6502.I, true);
                push((byte)status);

                // Read new PC location from fixed address
                addr_abs = ADDR_IRQ;
                ushort lo = read(addr_abs);
                ushort hi = read((ushort)(addr_abs + 1));
                pc = (ushort)((hi << 8) | lo);

                // IRQ cycles
                cycles = 7;
            }
        }

        /// <summary>
        /// Non-maskable Interrupt Request
        /// </summary>
        /// <remarks>
        /// A Non-Maskable Interrupt cannot be ignored. It behaves in exactly the
        /// same way as a regular IRQ, but reads the new program counter address
        /// from location 0xFFFA.
        /// </remarks>
        public void NMI()
        {
            // Push the PC to the stack. It is 16-bits, so requires 2 pushes
            push((byte)((pc >> 8) & 0x00FF));
            push((byte)(pc & 0x00FF));

            // Then push status register to the stack
            setFlag(FLAGS6502.B, false);
            setFlag(FLAGS6502.U, true);
            setFlag(FLAGS6502.I, true);
            push((byte)status);

            // Read new PC location from fixed address
            addr_abs = ADDR_NMI;
            ushort lo = read(addr_abs);
            ushort hi = read((ushort)(addr_abs + 1));
            pc = (ushort)((hi << 8) | lo);

            // NMI cycles
            cycles = 8;
        }

        /// <summary>
        /// Perform clock cycle
        /// </summary>
        /// <remarks>
        /// Each instruction requires a variable number of clock cycles to execute.
        /// In my emulation, I only care about the final result and so I perform
        /// the entire computation in one hit. In hardware, each clock cycle would
        /// perform "microcode" style transformations of the CPUs state.
        ///
        /// To remain compliant with connected devices, it's important that the 
        /// emulation also takes "time" in order to execute instructions, so I
        /// implement that delay by simply counting down the cycles required by 
        /// the instruction. When it reaches 0, the instruction is complete, and
        /// the next one is ready to be executed.
        /// </remarks>
        public void Clock()
        {
            if (cycles == 0)
            {
                // Read the next instruction byte. This 8-bit value is used to index the translation
                // table to get the relevat information about how to implement the instruction
                opcode = read(pc);

                // Make sure Unused status flag is 1
                setFlag(FLAGS6502.U, true);

                // After reading opcode, increment pc
                pc++;

                // Get starting number of cycles
                cycles = opcode_lookup[opcode].cycles;

                // Perform fetch of intermediate data using the required addressing mode
                byte additional_cycle1 = opcode_lookup[opcode].addr_mode();

                // Perform operation
                byte additional_cycle2 = opcode_lookup[opcode].operation();

                // Add additional cycles that may be required to complete operation
                cycles += (byte)(additional_cycle1 & additional_cycle2);

                // Make sure Unused status flag is 1
                setFlag(FLAGS6502.U, true);
            }

            clock_count++;

            cycles--;
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

        #region Emulator vars
        private byte   fetched      = 0x00;     // Represents the working input value to the ALU
        private ushort temp         = 0x0000;   // Just a temp var
        private ushort addr_abs     = 0x0000;   // Absolute memory address
        private ushort addr_rel     = 0x0000;   // Relative memory address
        private byte   opcode       = 0x00;     // Current instruction
        private byte   cycles       = 0;        // Counts how many cycles the instruction has remaining
        private uint   clock_count  = 0;        // A global accumulation of the number of clocks
        #endregion // Emulator vars

        /// <summary>
        /// The read location of data can come from two sources:
        /// A memory address or its immediately available as part of the instruction.
        /// This function decides depending on the address mode of the instruction byte.
        /// </summary>
        /// <returns></returns>
        private byte fetch()
        {
            if (!(opcode_lookup[opcode].addr_mode == IMP))
                fetched = read(addr_abs);

            return fetched;
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

        /// <summary>
        /// Reads an 8-bit byte from the bus, located at the specified 16-bit address
        /// </summary>
        /// <param name="addr">Address of the byte to read</param>
        /// <returns></returns>
        private byte read(ushort addr)
        {
            // In normal operation "read only" is set to false. This may seem odd. Some
            // devices on the bus may change state when they are read from, and this 
            // is intentional under normal circumstances. However the disassembler will
            // want to read the data at an address without changing the state of the
            // devices on the bus
            return bus.Read(addr, false);
        }

        /// <summary>
        /// Writes a byte to the bus at the specified address
        /// </summary>
        /// <param name="addr">Address to write to</param>
        /// <param name="data">The byte of data to write</param>
        private void write(ushort addr, byte data)
        {
            bus.Write(addr, data);
        }

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
         *****/

        /// <summary>
        /// Address Mode: Implied
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// There is no additional data required for this instruction. The instruction
        /// does something very simple like like sets a status bit. However, we will
        /// target the accumulator, for instructions like PHA
        /// </remarks>
        private byte IMP()
        {
            fetched = a;
            return 0;
        }

        /// <summary>
        /// Address Mode: Immediate
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The instruction expects the next byte to be used as a value, so we'll prep
        /// the read address to point to the next byte
        /// </remarks>
        private byte IMM()
        {
            addr_abs = pc++;
            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// To save program bytes, zero page addressing allows you to absolutely address
        /// a location in first 0xFF bytes of address range. Clearly this only requires
        /// one byte instead of the usual two.
        /// </remarks>
        private byte ZP0()
        {
            addr_abs = read(pc);
            pc++;
            addr_abs &= 0x00FF;
            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page with X Offset
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Fundamentally the same as Zero Page addressing, but the contents of the X Register
        /// is added to the supplied single byte address. This is useful for iterating through
        /// ranges within the first page.
        /// </remarks>
        private byte ZPX()
        {
            addr_abs = (ushort)(read(pc) + this.x);
            pc++;
            addr_abs &= 0x00ff;

            return 0;
        }

        /// <summary>
        /// Address Mode: Zero Page with Y Offset
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Same as Zero Page with X offset, but uses Y register for offset
        /// </remarks>
        private byte ZPY()
        {
            addr_abs = (ushort)(read(pc) + this.y);
            pc++;
            addr_abs &= 0x00ff;

            return 0;
        }

        /// <summary>
        /// Address Mode: Relative
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// This address mode is exclusive to branch instructions. The address
        /// must reside within -128 to +127 of the branch instruction, i.e.
        /// you cant directly branch to any address in the addressable range.
        /// </remarks>
        private byte REL()
        {
            addr_rel = read(pc);
            pc++;
            if ((addr_rel & 0x80) == 1)
                addr_rel |= 0xFF00;

            return 0;
        }

        /// <summary>
        /// Address Mode: Absolute
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// A full 16-bit address is loaded and used
        /// </remarks>
        private byte ABS()
        {
            ushort lo = read(pc);
            pc++;
            ushort hi = read(pc);
            pc++;

            addr_abs = (ushort)((hi << 8) | lo);

            return 0;
        }

        /// <summary>
        /// Address Mode: Absolute with X Offset
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Fundamentally the same as absolute addressing, but the contents of the X Register
        /// is added to the supplied two byte address. If the resulting address changes
        /// the page, an additional clock cycle is required
        /// </remarks>
        private byte ABX()
        {
            ushort lo = read(pc);
            pc++;
            ushort hi = read(pc);
            pc++;

            addr_abs = (ushort)((hi << 8) | lo);
            addr_abs += x;

            if ((addr_abs & 0xFF00) != (hi << 8))
                return 1;
            else
                return 0;
        }

        /// <summary>
        /// Address Mode: Absolute with Y Offset
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Fundamentally the same as absolute addressing, but the contents of the Y Register
        /// is added to the supplied two byte address. If the resulting address changes
        /// the page, an additional clock cycle is required
        /// </remarks>
        private byte ABY()
        {
            ushort lo = read(pc);
            pc++;
            ushort hi = read(pc);
            pc++;

            addr_abs = (ushort)((hi << 8) | lo);
            addr_abs += y;

            if ((addr_abs & 0xFF00) != (hi << 8))
                return 1;
            else
                return 0;
        }

        /// <summary>
        /// Address Mode: Indirect
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The supplied 16-bit address is read to get the actual 16-bit address. This 
        /// instruction is unusual in that it has a bug in the hardware! To emulate its
        /// function accurately, we also need to emulate this bug. If the low byte of the
        /// supplied address is 0xFF, then to read the high byte of the actual address
        /// we need to cross a page boundary. This doesnt actually work on the chip as 
        /// designed, instead it wraps back around in the same page, yielding an 
        /// invalid actual address
        /// </remarks>
        private byte IND()
        {
            ushort ptr_lo = read(pc);
            pc++;
            ushort ptr_hi = read(pc);
            pc++;

            ushort ptr = (ushort)((ptr_hi << 8) | ptr_lo);

            ushort lo = read(ptr);

            // Simulate page boundary hardware bug
            if (ptr_lo == 0x00FF)
            {
                addr_abs = (ushort)((read((ushort)(ptr & 0xFF00)) << 8) | lo);
            }
            else
            {
                // Normal behavior
                addr_abs = (ushort)((read((ushort)(ptr + 1)) << 8) | lo);
            }

            return 0;
        }

        /// <summary>
        /// Address Mode: Indirect X
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The supplied 8-bit address is offset by X Register to index a location in page 0x00. 
        /// The actual 16-bit address is read from this location
        /// </remarks>
        private byte IZX()
        {
            ushort t = read(pc);
            pc++;

            ushort lo = read((ushort)((ushort)(t + x) & 0x00FF));
            ushort hi = read((ushort)((ushort)(t + x + 1) & 0x00FF));

            addr_abs = (ushort)((hi << 8) | lo);

            return 0;
        }

        /// <summary>
        /// Address Mode: Indirect Y
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// The supplied 8-bit address indexes a location in page 0x00. From here the actual 
        /// 16-bit address is read, and the contents of the Y Register is added to it to offset 
        /// it. If the offset causes a change in page then an additional clock cycle is required.
        /// </remarks>
        private byte IZY()
        {
            ushort t = read(pc);
            pc++;

            ushort lo = read((ushort)(t & 0x00FF));
            ushort hi = read((ushort)((ushort)(t + 1) & 0x00FF));

            addr_abs = (ushort)((hi << 8) | lo);
            addr_abs += y;

            if ((addr_abs & 0xFF00) != (hi << 8))
                return 1;
            else
                return 0;
        }

        #endregion // Addressing Modes

        #region OpCodes
        /*****
         * There are 56 "legitimate" opcodes provided by the 6502 CPU. I have not modelled "unofficial" opcodes. As each opcode is 
         * defined by 1 byte, there are potentially 256 possible codes. Codes are not used in a "switch case" style on a processor,
         * instead they are repsonisble for switching individual parts of CPU circuits on and off. The opcodes listed here are official, 
         * meaning that the functionality of the chip when provided with these codes is as the developers intended it to be. Unofficial
         * codes will of course also influence the CPU circuitry in interesting ways, and can be exploited to gain additional
         * functionality!
         * 
         * These functions return 0 normally, but some are capable of requiring more clock cycles when executed under certain
         * conditions combined with certain addressing modes. If that is the case, they return 1.
         *****/

        private byte ADC()
        {
            throw new NotImplementedException();
        }

        private byte AND()
        {
            throw new NotImplementedException();
        }

        private byte ASL()
        {
            throw new NotImplementedException();
        }

        private byte BCC()
        {
            throw new NotImplementedException();
        }

        private byte BCS()
        {
            throw new NotImplementedException();
        }

        private byte BEQ()
        {
            throw new NotImplementedException();
        }

        private byte BIT()
        {
            throw new NotImplementedException();
        }

        private byte BMI()
        {
            throw new NotImplementedException();
        }

        private byte BNE()
        {
            throw new NotImplementedException();
        }

        private byte BPL()
        {
            throw new NotImplementedException();
        }

        private byte BRK()
        {
            throw new NotImplementedException();
        }

        private byte BVC()
        {
            throw new NotImplementedException();
        }

        private byte BVS()
        {
            throw new NotImplementedException();
        }

        private byte CLC()
        {
            throw new NotImplementedException();
        }

        private byte CLD()
        {
            throw new NotImplementedException();
        }

        private byte CLI()
        {
            throw new NotImplementedException();
        }

        private byte CLV()
        {
            throw new NotImplementedException();
        }

        private byte CMP()
        {
            throw new NotImplementedException();
        }

        private byte CPX()
        {
            throw new NotImplementedException();
        }

        private byte CPY()
        {
            throw new NotImplementedException();
        }

        private byte DEC()
        {
            throw new NotImplementedException();
        }

        private byte DEX()
        {
            throw new NotImplementedException();
        }

        private byte DEY()
        {
            throw new NotImplementedException();
        }

        private byte EOR()
        {
            throw new NotImplementedException();
        }

        private byte INC()
        {
            throw new NotImplementedException();
        }

        private byte INX()
        {
            throw new NotImplementedException();
        }

        private byte INY()
        {
            throw new NotImplementedException();
        }

        private byte JMP()
        {
            throw new NotImplementedException();
        }

        private byte JSR()
        {
            throw new NotImplementedException();
        }

        private byte LDA()
        {
            throw new NotImplementedException();
        }

        private byte LDX()
        {
            throw new NotImplementedException();
        }

        private byte LDY()
        {
            throw new NotImplementedException();
        }

        private byte LSR()
        {
            throw new NotImplementedException();
        }

        private byte NOP()
        {
            throw new NotImplementedException();
        }

        private byte ORA()
        {
            throw new NotImplementedException();
        }

        private byte PHA()
        {
            throw new NotImplementedException();
        }

        private byte PHP()
        {
            throw new NotImplementedException();
        }

        private byte PLA()
        {
            throw new NotImplementedException();
        }

        private byte PLP()
        {
            throw new NotImplementedException();
        }

        private byte ROL()
        {
            throw new NotImplementedException();
        }

        private byte ROR()
        {
            throw new NotImplementedException();
        }

        private byte RTI()
        {
            throw new NotImplementedException();
        }

        private byte RTS()
        {
            throw new NotImplementedException();
        }

        private byte SBC()
        {
            throw new NotImplementedException();
        }

        private byte SEC()
        {
            throw new NotImplementedException();
        }

        private byte SED()
        {
            throw new NotImplementedException();
        }

        private byte SEI()
        {
            throw new NotImplementedException();
        }

        private byte STA()
        {
            throw new NotImplementedException();
        }

        private byte STX()
        {
            throw new NotImplementedException();
        }

        private byte STY()
        {
            throw new NotImplementedException();
        }

        private byte TAX()
        {
            throw new NotImplementedException();
        }

        private byte TAY()
        {
            throw new NotImplementedException();
        }

        private byte TSX()
        {
            throw new NotImplementedException();
        }

        private byte TXA()
        {
            throw new NotImplementedException();
        }

        private byte TXS()
        {
            throw new NotImplementedException();
        }

        private byte TYA()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// All "unofficial" opcodes will be routed here.
        /// </summary>
        /// <returns></returns>
        private byte XXX()
        {
            throw new NotImplementedException();
        }
        #endregion // OpCodes

        private void build_lookup()
        {
            this.opcode_lookup = new List<Instruction>() {
                new Instruction() { name = "BRK", operation = BRK, addr_mode = IMM, cycles = 7 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "PHP", operation = PHP, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BPL", operation = BPL, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "CLC", operation = CLC, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "JSR", operation = JSR, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "AND", operation = AND, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "BIT", operation = BIT, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "PLP", operation = PLP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "BIT", operation = BIT, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BMI", operation = BMI, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "AND", operation = AND, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "SEC", operation = SEC, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "RTI", operation = RTI, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "PHA", operation = PHA, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "JMP", operation = JMP, addr_mode = ABS, cycles = 3 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BVC", operation = BVC, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "CLI", operation = CLI, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "RTS", operation = RTS, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "PLA", operation = PLA, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "JMP", operation = JMP, addr_mode = IND, cycles = 5 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BVS", operation = BVS, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "SEI", operation = SEI, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "STA", operation = STA, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "DEY", operation = DEY, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "TXA", operation = TXA, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "BCC", operation = BCC, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "STA", operation = STA, addr_mode = IZY, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ZPY, cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "TYA", operation = TYA, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABY, cycles = 5 },
                new Instruction() { name = "TXS", operation = TXS, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABX, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 3 },
                new Instruction() { name = "TAY", operation = TAY, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "TAX", operation = TAX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "BCS", operation = BCS, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ZPY, cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "CLV", operation = CLV, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "TSX", operation = TSX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "INY", operation = INY, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "DEX", operation = DEX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BNE", operation = BNE, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "CLD", operation = CLD, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "CPX", operation = CPX, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IZX, cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "CPX", operation = CPX, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ZP0, cycles = 3 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ZP0, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 5 },
                new Instruction() { name = "INX", operation = INX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IMM, cycles = 2 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = SBC, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "CPX", operation = CPX, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABS, cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ABS, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "BEQ", operation = BEQ, addr_mode = REL, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IZY, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ZPX, cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ZPX, cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 6 },
                new Instruction() { name = "SED", operation = SED, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABY, cycles = 4 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, cycles = 4 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABX, cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ABX, cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, cycles = 7 }
            };
        }

        private void push(byte data)
        {
            write((ushort)(ADDR_STACK + sp), data);
            sp--;
        }

#if LOGMODE
        // private Log log;
#endif
    }
}
