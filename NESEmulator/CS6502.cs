using System;
using System.Collections.Generic;
using log4net;

namespace NESEmulator
{
    [Flags]
    public enum FLAGS6502
    {
        /// <summary>
        /// Carry
        /// </summary>
        C = (1 << 0),
        /// <summary>
        /// Zero
        /// </summary>
        Z = (1 << 1),
        /// <summary>
        /// Interrupt disable
        /// </summary>
        I = (1 << 2),
        /// <summary>
        /// Decimal (unused right now)
        /// </summary>
        D = (1 << 3),
        /// <summary>
        /// Break
        /// </summary>
        B = (1 << 4),
        /// <summary>
        /// Unused
        /// </summary>
        U = (1 << 5),
        /// <summary>
        /// Overflow
        /// </summary>
        V = (1 << 6),
        /// <summary>
        /// Negative
        /// </summary>
        N = (1 << 7)
    }

    public class CS6502 : InterruptableBusDevice
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CS6502));

        public override BusDeviceType DeviceType { get { return BusDeviceType.CPU; } }

        private List<Instruction> opcode_lookup;
        private IBus bus;

        private bool _nmiPending = false;
        private bool _irqPending = false;
        private bool _irqDisablePending = false;
        private byte _irqEnableLatency = 0;
        private byte _irqDisableLatency = 0;
        private bool _startCountingIRQs = true;
        private int  _irqCount = 0;

        #region Well-Known Addresses

        /// <summary>
        /// Address of bottom of stack
        /// </summary>
        public const ushort ADDR_STACK = 0x0100;

        /// <summary>
        /// Address of program counter
        /// </summary>
        public const ushort ADDR_PC = 0xFFFC;
        
        /// <summary>
        /// Address of code for IRQ
        /// </summary>
        public const ushort ADDR_IRQ = 0xFFFE;

        /// <summary>
        /// Address of code for NMI
        /// </summary>
        public const ushort ADDR_NMI = 0xFFFA;

        #endregion // Well-Known Addresses

        #region DMA Attributes

        /// <summary>
        /// Indicates if OAMDMA transfer is in progress
        /// </summary>
        public bool DMATransfer { get; private set; }
        private byte _dmaPage;
        private byte _dmaStartAddr;
        private byte _dmaAddr;
        private byte _dmaData;
        private bool _dmaSync = false;

        #endregion // DMA Attributes

        #region Memory Reader Attributes

        public MemoryReader ExternalMemoryReader;
        private bool _readerFetch = false;

        #endregion // DMC Attributes

        public CS6502()
        {
            build_lookup();
            ExternalMemoryReader = new MemoryReader();
            ExternalMemoryReader.MemoryReadRequest += ExternalMemoryReader_MemoryReadRequest;

            // https://wiki.nesdev.org/w/index.php?title=CPU_power_up_state
            // "P = $34"
            status = FLAGS6502.I | FLAGS6502.B | FLAGS6502.U;
        }

        private void ExternalMemoryReader_MemoryReadRequest(object sender, EventArgs e)
        {
            _readerFetch = true;
            ExternalMemoryReader.CyclesToComplete = 4;
        }

        public void SignalNMI() => _nmiPending = true;
        public void SignalIRQ() => _irqPending = true;
        public void ClearIRQ() => _irqPending = false;

        public override void HandleInterrupt(object sender, InterruptEventArgs e)
        {
            //if (!(sender is CS2A03) && !(sender is CS2C02 && e.Interrupt == InterruptType.NMI))
            //    Log.Debug($"Handling {e.Interrupt} from {sender}");
            switch (e.Interrupt)
            {
                case InterruptType.NMI:
                    _nmiPending = true;
                    break;

                case InterruptType.IRQ:
                    _irqPending = true;
                    //Log.Debug($"[{clock_count}] IRQ signal received");
                    break;

                case InterruptType.CLEAR_IRQ:
                    _irqPending = false;
                    //Log.Debug($"[{clock_count}] Clear IRQ signal received");
                    break;
            }
        }

        public void ConnectBus(IBus bus)
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


        /// <summary>
        /// Reset CPU to known state
        /// </summary>
        /// <remarks>
        /// This is hard-wired inside the CPU. The status register remains the same except for unused
        /// bit which remains at 1, and interrupt inhibit which is set to 1 as well. An 
        /// absolute address is read from location 0xFFFC
        /// which contains a second address that the program counter is set to. This 
        /// allows the programmer to jump to a known and programmable location in the
        /// memory to start executing from. Typically the programmer would set the value
        /// at location 0xFFFC at compile time.
        /// </remarks>
        public override void Reset()
        {
            // Reset DMA
            _dmaAddr = 0;
            _dmaData = 0;
            _dmaPage = 0;
            _dmaSync = false;
            DMATransfer = false;

            // Set PC
            addr_abs = ADDR_PC;
            ushort lo = read(addr_abs);
            ushort hi = read((ushort)(addr_abs + 1));
            pc = (ushort)((hi << 8) | lo);
            Log.Info($"Cartridge starts at {pc:X4}");

            // Reset internal registers
            a = x = y = 0;
            sp = 0xFD;
            status |= (FLAGS6502.U | FLAGS6502.I);

            // Clear internal helper variables
            addr_rel = addr_abs = 0x0000;
            fetched = 0x00;

            _irqPending = false;
            _nmiPending = false;

            // Reset takes time
            cycles = 8;
        }

        /// <summary>
        /// Interrupt Request
        /// </summary>
        /// <remarks>
        /// Interrupt requests are a complex operation and only happen if the
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
            //if (_startCountingIRQs)
            //{
            //    _irqCount++;
            //    Log.Debug($"IRQCount = {_irqCount}");
            //}

            // Push the PC to the stack. It is 16-bits, so requires 2 pushes
            push((byte)((pc >> 8) & 0x00FF));
            push((byte)(pc & 0x00FF));

            // Then push status register to the stack
            setFlag(FLAGS6502.B, false);
            setFlag(FLAGS6502.U, true);
            push((byte)status);
            setFlag(FLAGS6502.I, true);

            // Read new PC location from fixed address
            addr_abs = ADDR_IRQ;
            ushort lo = read(addr_abs);
            ushort hi = read((ushort)(addr_abs + 1));
            // NOTE: It is my understanding that on the original hardware, the vector address
            // can in the middle of this instruction get hijacked by NMIs and point the PC to the
            // NMI handler. We do not do cycle-by-cycle emulation currently, so this cannot happen
            // at this time. It can possibly be done with our method with a little refactoring though....
            pc = (ushort)((hi << 8) | lo);

            Log.Debug($"[{clock_count}] IRQ invoked - will enter handler at [{clock_count + 7}]");
            // IRQ cycles
            cycles = 7;
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

            _nmiPending = false;

            // NMI cycles
            cycles = 8;
        }

        /// <summary>
        /// Who knew interrupting code could be so complicated?
        /// </summary>
        /// <param name="midInstructionCycle">
        /// True if we are calling this function in the middle of an instruction, false if beginning.
        /// This is important because we do not want to alter intermediate states between instructions.
        /// </param>
        private bool pollForIRQ(bool midInstructionCycle = false)
        {
            // OMG!! I know you don't want to be interrupted, Mr. CPU - but I JUST asked for an IRQ!!
            if (getFlag(FLAGS6502.I) == 1 && _irqPending && _irqDisablePending)
            {
                if (!midInstructionCycle)
                    _irqDisablePending = false;
                // Fine - this is the LAST ONE. After this, I'm cutting you off!
                //IRQ();
                return true;
            }
            else if (getFlag(FLAGS6502.I) == 0)
            {
                if (_irqPending && _irqEnableLatency == 0)
                {
                    //IRQ();
                    return true;
                }

                if (!midInstructionCycle && _irqEnableLatency > 0)
                    --_irqEnableLatency;

                return false;
            }

            if (!midInstructionCycle)
                _irqDisablePending = false;

            return false;
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
        public override void Clock(ulong clockCounter)
        {
            if (clockCounter % 3 != 0)
                return;

            clock_count++;

            // Read in another code-base that instructions don't start executing until after cpu cycles, so here we go.
            if (clock_count <= 8) return;

            if (DMATransfer)
            {
                doDMATransfer(clockCounter);
            }
            else if (_readerFetch)
            {
                readerFetch(clockCounter);
            }
            else
            {
                //if (_nmiPending && pollForIRQ(true) && cycles < 5)
                //{
                //    // An NMI has hijacked the IRQ/BRK vector. Set PC to NMI vector.
                //    addr_abs = ADDR_NMI;
                //    ushort lo = read(addr_abs);
                //    ushort hi = read((ushort)(addr_abs + 1));
                //    pc = (ushort)((hi << 8) | lo);
                //    Log.Debug($"[{clock_count}] IRQ/BRK has been hijacked by NMI!");
                //}

                if (cycles == 0)
                {
                    if (_nmiPending)
                    {
                        //Log.Debug($"[{clock_count}] Invoking NMI");
                        NMI();
                    }
                    else if (pollForIRQ())
                    {
                        IRQ();
                    }
                    else
                    {
                        //var (addr, sInst) = Disassemble(pc);
                        //Log.Debug($"[{clock_count}] {sInst}");

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
                }

                //clock_count++;

                cycles--;
            }
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
            uint addr = start;
            Dictionary<ushort, string> mapLines = new Dictionary<ushort, string>();
            ushort line_addr = 0;

            // Starting at the specified address we read an instruction
            // byte, which in turn yields information from the lookup table
            // as to how many additional bytes we need to read and what the
            // addressing mode is. I need this info to assemble human readable
            // syntax, which is different depending upon the addressing mode

            // As the instruction is decoded, a std::string is assembled
            // with the readable output
            while (addr <= stop)
            {
                line_addr = (ushort)addr;

                string sInst;
                (addr, sInst) = Disassemble((ushort)addr);

                // Add the formed string to a Dictionary, using the instruction's
                // address as the key. This makes it convenient to look for later
                // as the instructions are variable in length, so a straight up
                // incremental index is not sufficient.
                mapLines[line_addr] = sInst;
            }

            return mapLines;
        }

        public (ushort, string) Disassemble(ushort addr)
        {
            byte value = 0x00, lo = 0x00, hi = 0x00;

            string hexAddr = addr.ToString("X4");

            // Read instruction, and get its readable name
            byte opcode = bus.Read((ushort)addr, true);
            addr++;

            // Get oprands from desired locations, and form the
            // instruction based upon its addressing mode. These
            // routines mimmick the actual fetch routine of the
            // 6502 in order to get accurate data as part of the
            // instruction
            string addressMode = string.Empty;
            if (opcode_lookup[opcode].addr_mode == IMP)
            {
                addressMode = "{IMP}";
            }
            else if (opcode_lookup[opcode].addr_mode == IMM)
            {
                value = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("#${0} {{IMM}}", value.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == ZP0)
            {
                lo = bus.Read((ushort)addr, true);
                hi = 0x00;
                addr++;
                addressMode = string.Format("${0} {{ZP0}}", lo.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == ZPX)
            {
                lo = bus.Read((ushort)addr, true);
                hi = 0x00;
                addr++;
                addressMode = string.Format("${0}, X {{ZPX}}", lo.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == ZPY)
            {
                lo = bus.Read((ushort)addr, true);
                hi = 0x00;
                addr++;
                addressMode = string.Format("${0}, Y {{ZPY}}", lo.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == IZX)
            {
                lo = bus.Read((ushort)addr, true);
                hi = 0x00;
                addr++;
                addressMode = string.Format("(${0}, X) {{IZX}}", lo.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == IZY)
            {
                lo = bus.Read((ushort)addr, true);
                hi = 0x00;
                addr++;
                addressMode = string.Format("(${0}), Y {{IZY}}", lo.ToString("X2"));
            }
            else if (opcode_lookup[opcode].addr_mode == ABS)
            {
                lo = bus.Read((ushort)addr, true);
                addr++;
                hi = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("${0} {{ABS}}", ((hi << 8) | lo).ToString("X4"));
            }
            else if (opcode_lookup[opcode].addr_mode == ABX)
            {
                lo = bus.Read((ushort)addr, true);
                addr++;
                hi = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("${0}, X {{ABX}}", ((hi << 8) | lo).ToString("X4"));
            }
            else if (opcode_lookup[opcode].addr_mode == ABY)
            {
                lo = bus.Read((ushort)addr, true);
                addr++;
                hi = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("${0}, Y {{ABY}}", ((hi << 8) | lo).ToString("X4"));
            }
            else if (opcode_lookup[opcode].addr_mode == IND)
            {
                lo = bus.Read((ushort)addr, true);
                addr++;
                hi = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("(${0}) {{IND}}", ((hi << 8) | lo).ToString("X4"));
            }
            else if (opcode_lookup[opcode].addr_mode == REL)
            {
                value = bus.Read((ushort)addr, true);
                addr++;
                addressMode = string.Format("${0} [${1}] {{REL}}", value.ToString("X2"), (addr + (sbyte)value).ToString("X4"));
            }

            string sInst = string.Format("$ {0}: {1} {2}", hexAddr, opcode_lookup[opcode].name, addressMode);

            return (addr, sInst);
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

        private void doDMATransfer(ulong clockCounter)
        {
            if (!_dmaSync)
            {
                if (clockCounter % 2 == 1)
                {
                    _dmaSync = true;
                    _dmaStartAddr = read(0x2003);
                    _dmaAddr = _dmaStartAddr;
                }
            }
            else
            {
                if (clockCounter % 2 == 0)
                {
                    _dmaData = read((ushort)(_dmaPage << 8 | _dmaAddr));
                }
                else
                {
                    // So yeah, let's just break all encapsulation and grab that PPU. Kinda what the HW is doing, I suppose...
                    ((NESBus)bus).PPU.OAM[_dmaAddr >> 2][_dmaAddr & 0x03] = _dmaData;
                    _dmaAddr++;

                    if (_dmaAddr == _dmaStartAddr)
                    {
                        DMATransfer = false;
                        _dmaSync = false;
                    }
                }
            }
        }

        private void readerFetch(ulong clockCounter)
        {
            if (clockCounter % 6 == 0)
            {
                ExternalMemoryReader.Buffer = read(ExternalMemoryReader.MemoryPtr);
                ExternalMemoryReader.BufferReady = true;
                this.cycles = ExternalMemoryReader.CyclesToComplete;
                _readerFetch = false;
            }
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

        public override bool Write(ushort addr, byte data)
        {
            // Ignore since we call BUS' Write(), which calls this.
            return false;
        }

        public override bool Read(ushort addr, out byte data)
        {
            // Ignore since we call BUS' Read(), which calls this.
            data = 0;
            return false;
        }

        /// <summary>
        /// Reads an 8-bit byte from the bus, located at the specified 16-bit address
        /// </summary>
        /// <param name="addr">Address of the byte to read</param>
        /// <returns></returns>
        private byte read(ushort addr)
        {
            byte dataRead = 0x00;
            // In normal operation "read only" is set to false. This may seem odd. Some
            // devices on the bus may change state when they are read from, and this 
            // is intentional under normal circumstances. However the disassembler will
            // want to read the data at an address without changing the state of the
            // devices on the bus

            dataRead = bus.Read(addr, false);

            return dataRead;
        }

        /// <summary>
        /// Writes a byte to the bus at the specified address
        /// </summary>
        /// <param name="addr">Address to write to</param>
        /// <param name="data">The byte of data to write</param>
        private void write(ushort addr, byte data)
        {
            if (addr == 0x4014)
            {
                _dmaPage = data;
                //_dmaAddr = 0x00;
                DMATransfer = true;
            }
            else
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
            addr_abs = read(pc);
            pc++;
            // The CPU performs a "dummy" read from the address here.
            Log.Debug($"[{clock_count}] Dummy read from addr_abs at 0x{addr_abs:X4}, index = 0x{x:X2}");
            read(addr_abs);
            addr_abs += x;
            addr_abs &= 0x00FF;

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
            addr_abs = read(pc);
            pc++;
            // The CPU performs a "dummy" read from the address here.
            Log.Debug($"[{clock_count}] Dummy read from addr_abs at 0x{addr_abs:X4}, index = 0x{y:X2}");
            read(addr_abs);
            addr_abs += y;
            addr_abs &= 0x00FF;

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
            if ((addr_rel & 0x80) != 0)
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
            byte lo = read(pc);
            pc++;
            byte hi = read(pc);
            pc++;

            addr_abs = (ushort)((hi << 8) | lo);
            ushort addr_eff = (ushort)((hi << 8) | (lo + x));
            addr_abs += x;

            if ((addr_abs & 0xFF00) != (hi << 8) || opcode_lookup[opcode].instr_type == CPUInstructionType.R_M_W)
            {
                // perform "dummy read"
                Log.Debug($"[{clock_count}] {{ABX}} Dummy read from addr_eff at 0x{addr_eff:X4}. addr_abs = 0x{addr_abs:X4}, index = 0x{x:X2}");
                read(addr_eff);
                return 1;
            }
            else
            {
                return 0;
            }
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
            ushort addr_eff = (ushort)((hi << 8) | (lo + y));
            addr_abs += y;

            if ((addr_abs & 0xFF00) != (hi << 8))
            {
                // perform "dummy read"
                Log.Debug($"[{clock_count}] Dummy read from addr_eff at 0x{addr_eff:X4}. addr_abs = 0x{addr_abs:X4}, index = 0x{y:X2}");
                read(addr_eff);
                return 1;
            }
            else
            {
                return 0;
            }
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
            ushort ptr = read(pc);
            pc++;

            // According to https://github.com/iliak/nes/blob/master/doc/6502_cpu.txt#L1191, it
            // seems like the below should work but it causes dummy read tests to fail.
            //ushort addr_eff = (ushort)((read(ptr) + x) & 0x00FF);
            //ushort lo = read(addr_eff);
            //ushort hi = read((ushort)((addr_eff + 1) & 0x00FF));
            ushort lo = read((ushort)((ushort)(ptr + x) & 0x00FF));
            ushort hi = read((ushort)((ushort)(ptr + x + 1) & 0x00FF));

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
            ushort ptr = read(pc);
            pc++;

            ushort lo = read(ptr);
            ushort hi = read((ushort)((ushort)(ptr + 1) & 0x00FF));

            addr_abs = (ushort)((hi << 8) | lo);
            ushort addr_eff = (ushort)((hi << 8) | (lo + y));
            addr_abs += y;

            if ((addr_abs & 0xFF00) != (hi << 8))
            {
                // perform "dummy read"
                Log.Debug($"[{clock_count}] {{IZY}} Dummy read from addr_eff at 0x{addr_eff:X4}. addr_abs = 0x{addr_abs:X4}, index = 0x{y:X2}");
                read(addr_eff);
                return 1;
            }
            else
            {
                return 0;
            }
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

        /// <summary>
        /// Instruction: Add with Carry In
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Function:    A = A + M + C
        /// Flags Out:   C, V, N, Z
        /// </remarks>
        private byte ADC()
        {
            // Grab the data that we are adding to the accumulator
            fetch();

            // Add is performed in 16-bit domain for emulation to capture any
            // carry bit, which will exist in bit 8 of the 16-bit word
            temp = (ushort)(a + fetched + getFlag(FLAGS6502.C));

            // The carry flag out exists in the high byte bit 0
            setFlag(FLAGS6502.C, temp > 255);

            // The Zero flag is set if the result is 0
            testAndSet(FLAGS6502.Z, temp);

            // The signed Overflow flag is set based on all that up there! :D
            setFlag(FLAGS6502.V, ((~(a ^ fetched) & (a ^ temp)) & 0x0080) != 0);

            // The negative flag is set to the most significant bit of the result
            testAndSet(FLAGS6502.N, temp);

            // Load the result into the accumulator (it's 8-bit dont forget!)
            a = (byte)(temp & 0x00FF);

            // This instruction has the potential to require an additional clock cycle
            return 1;
        }

#region Bitwise Operators
        /// <summary>
        /// Instruction: Bitwise Logic AND
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Function:    A = A & M
        /// Flags Out:   N, Z
        /// </remarks>
        private byte AND()
        {
            fetch();
            a = (byte)(a & fetched);
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 1;
        }

        /// <summary>
        /// Instruction: Arithmetic Shift Left
        /// Function:    A = C <- (A << 1) <- 0
        /// Flags Out:   N, Z, C
        /// </summary>
        /// <returns></returns>
        private byte ASL()
        {
            fetch();
            temp = (ushort)(fetched << 1);
            setFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            if (opcode_lookup[opcode].addr_mode == IMP)
            {
                a = (byte)(temp & 0x00FF);
            }
            else
            {
                write(addr_abs, fetched);   // write original value first
                write(addr_abs, (byte)(temp & 0x00FF));
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Test memory bits with accumulator
        /// Flags Out:   Z, N, V
        /// </summary>
        /// <returns></returns>
        private byte BIT()
        {
            fetch();
            temp = (ushort)(a & fetched);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, fetched);
            setFlag(FLAGS6502.V, (fetched & (1 << 6)) != 0);
            return 0;
        }

        /// <summary>
        /// Instruction: Shift one bit right
        /// Function:    A = 0 -> (A >> 1) -> C
        /// Flags Out:   C, Z, N
        /// </summary>
        /// <returns></returns>
        private byte LSR()
        {
            fetch();
            setFlag(FLAGS6502.C, (fetched & 0x0001) == 1);
            temp = (ushort)(fetched >> 1);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            if (opcode_lookup[opcode].addr_mode == IMP)
                a = (byte)(temp & 0x00FF);
            else
            {
                write(addr_abs, fetched);   // write original value first
                write(addr_abs, (byte)(temp & 0x00FF));
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Bitwise Logic XOR
        /// Function:    A = A xor M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte EOR()
        {
            fetch();
            a = (byte)(a ^ fetched);
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 1;
        }

        /// <summary>
        /// Instruction: Bitwise Logic OR
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Function:    A = A | M
        /// Flags Out:   N, Z
        /// </remarks>
        private byte ORA()
        {
            fetch();
            a |= fetched;
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 1;
        }

        /// <summary>
        /// Instruction: Rotate One bit Left
        /// Function:    A or M = C <- (M << 1) <- C
        /// Flags Out:   C, Z, N
        /// </summary>
        /// <returns></returns>
        private byte ROL()
        {
            fetch();
            temp = (ushort)((fetched << 1) | getFlag(FLAGS6502.C));
            setFlag(FLAGS6502.C, (temp & 0xFF00) > 0);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            if (opcode_lookup[opcode].addr_mode == IMP)
                a = (byte)(temp & 0x00FF);
            else
            {
                write(addr_abs, fetched);   // write original value first
                write(addr_abs, (byte)(temp & 0x00FF));
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Rotate One bit Right
        /// Function:    A or M = C -> (M >> 1) -> C
        /// Flags Out:   C, Z, N
        /// </summary>
        /// <returns></returns>
        private byte ROR()
        {
            fetch();
            temp = (ushort)((getFlag(FLAGS6502.C) << 7) | fetched >> 1);
            setFlag(FLAGS6502.C, (fetched & 0x01) == 1);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            if (opcode_lookup[opcode].addr_mode == IMP)
                a = (byte)(temp & 0x00FF);
            else
            {
                write(addr_abs, fetched);   // write original value first
                write(addr_abs, (byte)(temp & 0x00FF));
            }
            return 0;
        }
#endregion // Bitwise Operators

#region Branch instructions

        /// <summary>
        /// Instruction: Branch if Carry Clear
        /// Function:    if(C == 0) pc = address 
        /// </summary>
        /// <returns></returns>
        private byte BCC()
        {
            if (getFlag(FLAGS6502.C) == 0)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Carry Set
        /// Function:    if(C == 1) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BCS()
        {
            if (getFlag(FLAGS6502.C) == 1)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Equal
        /// Function:    if(Z == 1) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BEQ()
        {
            if (getFlag(FLAGS6502.Z) == 1)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Negative
        /// Function:    if(N == 1) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BMI()
        {
            if (getFlag(FLAGS6502.N) == 1)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Not Equal
        /// Function:    if(Z == 0) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BNE()
        {
            if (getFlag(FLAGS6502.Z) == 0)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Positive
        /// Function:    if(N == 0) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BPL()
        {
            if (getFlag(FLAGS6502.N) == 0)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Overflow Clear
        /// Function:    if(V == 0) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BVC()
        {
            if (getFlag(FLAGS6502.V) == 0)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

        /// <summary>
        /// Instruction: Branch if Overflow Set
        /// Function:    if(V == 1) pc = address
        /// </summary>
        /// <returns></returns>
        private byte BVS()
        {
            if (getFlag(FLAGS6502.V) == 1)
            {
                cycles++;
                addr_abs = (ushort)(pc + addr_rel);

                if ((addr_abs & 0xFF00) != (pc & 0xFF00))
                    cycles++;

                pc = addr_abs;
            }
            return 0;
        }

#endregion // Branch instructions

        /// <summary>
        /// Instruction: Break
        /// Function:    Program Sourced Interrupt
        /// </summary>
        /// <returns></returns>
        private byte BRK()
        {
            pc++;

            setFlag(FLAGS6502.I, true);
            push((byte)((pc >> 8) & 0x00FF));
            push((byte)(pc & 0x00FF));

            setFlag(FLAGS6502.B, true);
            push((byte)status);
            setFlag(FLAGS6502.B, false);

            pc = (ushort)(read(0xFFFE) | (read(0xFFFF) << 8));
            return 0;
        }

#region Clear instructions

        /// <summary>
        /// Instruction: Clear Carry Flag
        /// Function:    C = 0
        /// </summary>
        /// <returns></returns>
        private byte CLC()
        {
            setFlag(FLAGS6502.C, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Clear Decimal Flag
        /// Function:    D = 0
        /// </summary>
        /// <returns></returns>
        private byte CLD()
        {
            setFlag(FLAGS6502.D, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Clear Overflow Flag
        /// Function:    V = 0
        /// </summary>
        /// <returns></returns>
        private byte CLV()
        {
            setFlag(FLAGS6502.V, false);
            return 0;
        }

#endregion // Clear instructions

        /// <summary>
        /// Instruction: Compare Accumulator
        /// Function:    C <- A >= M      Z <- (A - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        private byte CMP()
        {
            fetch();
            temp = (ushort)(a - fetched);
            setFlag(FLAGS6502.C, a >= fetched);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            return 1;
        }

        /// <summary>
        /// Instruction: Compare X Register
        /// Function:    C <- X >= M      Z <- (X - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        private byte CPX()
        {
            fetch();
            temp = (ushort)(x - fetched);
            setFlag(FLAGS6502.C, x >= fetched);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            return 0;
        }

        /// <summary>
        /// Instruction: Compare Y Register
        /// Function:    C <- Y >= M      Z <- (Y - M) == 0
        /// Flags Out:   N, C, Z
        /// </summary>
        /// <returns></returns>
        private byte CPY()
        {
            fetch();
            temp = (ushort)(y - fetched);
            setFlag(FLAGS6502.C, y >= fetched);
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            return 0;
        }

        /// <summary>
        /// Instruction: Decrement Value at Memory Location
        /// Function:    M = M - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte DEC()
        {
            fetch();
            write(addr_abs, fetched);   // write original value first
            temp = (ushort)(fetched - 1);
            write(addr_abs, (byte)(temp & 0x00FF));
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            return 0;
        }

        /// <summary>
        /// Instruction: Decrement X Register
        /// Function:    X = X - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte DEX()
        {
            x--;
            testAndSet(FLAGS6502.Z, x);
            testAndSet(FLAGS6502.N, x);
            return 0;
        }

        /// <summary>
        /// Instruction: Decrement Y Register
        /// Function:    Y = Y - 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte DEY()
        {
            y--;
            testAndSet(FLAGS6502.Z, y);
            testAndSet(FLAGS6502.N, y);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment Value at Memory Location
        /// Function:    M = M + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte INC()
        {
            fetch();
            write(addr_abs, fetched);   // write original value first
            temp = (ushort)(fetched + 1);
            write(addr_abs, (byte)(temp & 0x00FF));
            testAndSet(FLAGS6502.Z, temp);
            testAndSet(FLAGS6502.N, temp);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment X Register
        /// Function:    X = X + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte INX()
        {
            x++;
            testAndSet(FLAGS6502.Z, x);
            testAndSet(FLAGS6502.N, x);
            return 0;
        }

        /// <summary>
        /// Instruction: Increment Y Register
        /// Function:    Y = Y + 1
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte INY()
        {
            y++;
            testAndSet(FLAGS6502.Z, y);
            testAndSet(FLAGS6502.N, y);
            return 0;
        }

        /// <summary>
        /// Instruction: Jump To Location
        /// Function:    pc = address
        /// </summary>
        /// <returns></returns>
        private byte JMP()
        {
            pc = addr_abs;
            return 0;
        }

        /// <summary>
        /// Instruction: Jump To Sub-Routine
        /// Function:    PC -> stack, pc = address
        /// </summary>
        /// <returns></returns>
        private byte JSR()
        {
            pc--;
            push((byte)(pc >> 8));
            push((byte)(pc & 0x00FF));
            pc = addr_abs;
            return 0;
        }

#region Load instructions

        /// <summary>
        /// Instruction: Load The Accumulator
        /// Function:    A = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte LDA()
        {
            fetch();
            a = fetched;
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 1;
        }

        /// <summary>
        /// Instruction: Load The X Register
        /// Function:    X = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte LDX()
        {
            fetch();
            x = fetched;
            testAndSet(FLAGS6502.Z, x);
            testAndSet(FLAGS6502.N, x);
            return 1;
        }

        /// <summary>
        /// Instruction: Load The Y Register
        /// Function:    Y = M
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte LDY()
        {
            fetch();
            y = fetched;
            testAndSet(FLAGS6502.Z, y);
            testAndSet(FLAGS6502.N, y);
            return 1;
        }

#endregion // Load instructions

        /// <summary>
        /// No operation
        /// </summary>
        /// <returns></returns>
        /// <remarks>
        /// Sadly not all NOPs are equal, Ive added a few here
        /// based on https://wiki.nesdev.com/w/index.php/CPU_unofficial_opcodes
        /// and will add more based on game compatibility, and ultimately
        /// I'd like to cover all illegal opcodes too
        /// </remarks>
        private byte NOP()
        {
            Log.Debug($"[{clock_count}] NOP Opcode: 0x{opcode:X2}");
            switch (opcode)
            {
                case 0x1C:
                case 0x3C:
                case 0x5C:
                case 0x7C:
                case 0xDC:
                case 0xFC:
                    return 1;
            }
            return 0;
        }

        #region Stack Instructions
        /// <summary>
        /// Instruction: Push Accumulator to Stack
        /// Function:    A -> stack
        /// </summary>
        /// <returns></returns>
        private byte PHA()
        {
            push(a);
            return 0;
        }

        /// <summary>
        /// Instruction: Push Status Register to Stack
        /// Function:    FLAGS -> stack
        /// Flags Out:   B, U
        /// </summary>
        /// <returns></returns>
        private byte PHP()
        {
            write((ushort)(ADDR_STACK + sp), (byte)(status | FLAGS6502.B | FLAGS6502.U));
            setFlag(FLAGS6502.B, false);
            setFlag(FLAGS6502.U, false);
            sp--;
            return 0;
        }

        /// <summary>
        /// Instruction: Pop Accumulator off Stack
        /// Function:    A <- stack
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte PLA()
        {
            a = pop();
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 0;
        }

        /// <summary>
        /// Instruction: Pop Status Register off Stack
        /// Function:    FLAGS <- stack
        /// Flags Out:   U
        /// </summary>
        /// <returns>
        /// </returns>
        private byte PLP()
        {
            bool prevI = getFlag(FLAGS6502.I) == 1;
            status = (FLAGS6502)pop();
            if (!prevI && getFlag(FLAGS6502.I) == 1)
                _irqDisablePending = true;
            if (prevI && getFlag(FLAGS6502.I) == 0)
                _irqEnableLatency = 1;
            setFlag(FLAGS6502.U, true);
            return 0;
        }
        #endregion // Stack Instructions

        /// <summary>
        /// Instruction: Enable Interrupts / Clear Interrupt Disable Flag
        /// Function:    I = 0
        /// </summary>
        /// <returns></returns>
        private byte CLI()
        {
            //Log.Debug($"[{clock_count}] CLI");
            _startCountingIRQs = true;
            _irqCount = 0;
            if (getFlag(FLAGS6502.I) == 1)
            {
                _irqEnableLatency = 1;
            }
            setFlag(FLAGS6502.I, false);
            return 0;
        }

        /// <summary>
        /// Instruction: Set Interrupt Disable Flag / Disable Interrupts
        /// Function:    I = 1
        /// Flags Out:   I
        /// </summary>
        /// <returns></returns>
        private byte SEI()
        {
            if (getFlag(FLAGS6502.I) == 0)
            {
                _irqDisablePending = true;
                _startCountingIRQs = false;
            }

            setFlag(FLAGS6502.I, true);
            return 0;
        }

        /// <summary>
        /// Instruction: Return From Interrupt
        /// Function:    FLAGS <- stack, PC <- stack
        /// Flags Out:   B, U
        /// </summary>
        /// <returns></returns>
        private byte RTI()
        {
            status = (FLAGS6502)pop();
            status &= ~FLAGS6502.B;
            status &= ~FLAGS6502.U;

            pc = pop();
            pc |= (ushort)(pop() << 8);

            return 0;
        }

        /// <summary>
        /// Instruction: Return From Subroutine
        /// Function:    PC <- stack, PC = PC + 1
        /// Flags Out:   
        /// </summary>
        /// <returns></returns>
        private byte RTS()
        {
            pc = pop();
            pc |= (ushort)(pop() << 8);
            pc++;
            return 0;
        }

        /// <summary>
        /// Instruction: Subtraction with Borrow In
        /// Function:    A = A - M - (1 - C)
        /// Flags Out:   C, V, N, Z
        /// </summary>
        /// <returns></returns>
        private byte SBC()
        {
            fetch();

            // Operating in 16-bit domain to capture carry out

            // We can invert the bottom 8 bits with bitwise xor
            ushort value = (ushort)((fetched) ^ 0x00FF);

            // Notice this is exactly the same as addition from here!
            temp = (ushort)(a + value + getFlag(FLAGS6502.C));
            setFlag(FLAGS6502.C, (temp & 0xFF00) != 0);
            testAndSet(FLAGS6502.Z, temp);
            setFlag(FLAGS6502.V, ((temp ^ a) & (temp ^ value) & 0x0080) != 0);
            testAndSet(FLAGS6502.N, temp);
            a = (byte)(temp & 0x00FF);
            return 1;
        }

        /// <summary>
        /// Instruction: Set Carry Flag
        /// Function:    C = 1
        /// Flags Out:   C
        /// </summary>
        /// <returns></returns>
        private byte SEC()
        {
            setFlag(FLAGS6502.C, true);
            return 0;
        }

        /// <summary>
        /// Instruction: Set Decimal Flag
        /// Function:    D = 1
        /// Flags Out:   D
        /// </summary>
        /// <returns></returns>
        private byte SED()
        {
            setFlag(FLAGS6502.D, true);
            return 0;
        }

#region Store instructions

        /// <summary>
        /// Instruction: Store Accumulator at Address
        /// Function:    M = A
        /// Flags Out:
        /// </summary>
        /// <returns></returns>
        private byte STA()
        {
            write(addr_abs, a);
            return 0;
        }

        /// <summary>
        /// Instruction: Store X Register at Address
        /// Function:    M = X
        /// Flags Out:
        /// </summary>
        /// <returns></returns>
        private byte STX()
        {
            write(addr_abs, x);
            return 0;
        }

        /// <summary>
        /// Instruction: Store Y Register at Address
        /// Function:    M = Y
        /// Flags Out:
        /// </summary>
        /// <returns></returns>
        private byte STY()
        {
            write(addr_abs, y);
            return 0;
        }

#endregion // Store instructions

#region Transfer instructions

        /// <summary>
        /// Instruction: Transfer Accumulator to X Register
        /// Function:    X = A
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TAX()
        {
            x = a;
            testAndSet(FLAGS6502.Z, x);
            testAndSet(FLAGS6502.N, x);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Accumulator to Y Register
        /// Function:    Y = A
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TAY()
        {
            y = a;
            testAndSet(FLAGS6502.Z, y);
            testAndSet(FLAGS6502.N, y);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Stack Pointer to X Register
        /// Function:    X = sp
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TSX()
        {
            x = sp;
            testAndSet(FLAGS6502.Z, x);
            testAndSet(FLAGS6502.N, x);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer X Register to Accumulator
        /// Function:    A = X
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TXA()
        {
            a = x;
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer X Register to Stack Pointer
        /// Function:    SP = X
        /// Flags Out:
        /// </summary>
        /// <returns></returns>
        private byte TXS()
        {
            sp = x;
            return 0;
        }

        /// <summary>
        /// Instruction: Transfer Y Register to Accumulator
        /// Function:    A = Y
        /// Flags Out:   N, Z
        /// </summary>
        /// <returns></returns>
        private byte TYA()
        {
            a = y;
            testAndSet(FLAGS6502.Z, a);
            testAndSet(FLAGS6502.N, a);
            return 0;
        }

#endregion // Transfer instructions

        /// <summary>
        /// All "unofficial" opcodes will be routed here.
        /// </summary>
        /// <returns></returns>
        private byte XXX()
        {
            return 0;
        }
#endregion // OpCodes

        private void build_lookup()
        {
            this.opcode_lookup = new List<Instruction>() {
                new Instruction() { name = "BRK", operation = BRK, addr_mode = IMM, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "PHP", operation = PHP, addr_mode = IMP, instr_type = CPUInstructionType.R_W,     cycles = 3 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = IMP, instr_type = CPUInstructionType.R_M_W,   cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "BPL", operation = BPL, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0x10
                new Instruction() { name = "ORA", operation = ORA, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "CLC", operation = CLC, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x1C
                new Instruction() { name = "ORA", operation = ORA, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ASL", operation = ASL, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "JSR", operation = JSR, addr_mode = ABS, instr_type = CPUInstructionType.Branch,  cycles = 6 }, // 0x20
                new Instruction() { name = "AND", operation = AND, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "BIT", operation = BIT, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "PLP", operation = PLP, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = IMP, instr_type = CPUInstructionType.R_M_W,   cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "BIT", operation = BIT, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "BMI", operation = BMI, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0x30
                new Instruction() { name = "AND", operation = AND, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x34
                new Instruction() { name = "AND", operation = AND, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "SEC", operation = SEC, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "AND", operation = AND, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x3C
                new Instruction() { name = "AND", operation = AND, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROL", operation = ROL, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "RTI", operation = RTI, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 }, // 0x40
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 }, // 0x44
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "PHA", operation = PHA, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 3 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = IMP, instr_type = CPUInstructionType.R_M_W,   cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "JMP", operation = JMP, addr_mode = ABS, instr_type = CPUInstructionType.Branch,  cycles = 3 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "BVC", operation = BVC, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0x50
                new Instruction() { name = "EOR", operation = EOR, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x54
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "CLI", operation = CLI, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x5C
                new Instruction() { name = "EOR", operation = EOR, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LSR", operation = LSR, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "RTS", operation = RTS, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 }, // 0x60
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 }, // 0x64
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "PLA", operation = PLA, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = IMP, instr_type = CPUInstructionType.R_M_W,   cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "JMP", operation = JMP, addr_mode = IND, instr_type = CPUInstructionType.Branch,  cycles = 5 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "BVS", operation = BVS, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0x70
                new Instruction() { name = "ADC", operation = ADC, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x74
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "SEI", operation = SEI, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0x7C
                new Instruction() { name = "ADC", operation = ADC, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "ROR", operation = ROR, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0x80
                new Instruction() { name = "STA", operation = STA, addr_mode = IZX, instr_type = CPUInstructionType.Write,   cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ZP0, instr_type = CPUInstructionType.Write,   cycles = 3 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ZP0, instr_type = CPUInstructionType.Write,   cycles = 3 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ZP0, instr_type = CPUInstructionType.Write,   cycles = 3 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 3 },
                new Instruction() { name = "DEY", operation = DEY, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0x89
                new Instruction() { name = "TXA", operation = TXA, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ABS, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABS, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ABS, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "BCC", operation = BCC, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0x90
                new Instruction() { name = "STA", operation = STA, addr_mode = IZY, instr_type = CPUInstructionType.Write,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "STY", operation = STY, addr_mode = ZPX, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ZPX, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "STX", operation = STX, addr_mode = ZPY, instr_type = CPUInstructionType.Write,   cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "TYA", operation = TYA, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABY, instr_type = CPUInstructionType.Write,   cycles = 5 },
                new Instruction() { name = "TXS", operation = TXS, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "STA", operation = STA, addr_mode = ABX, instr_type = CPUInstructionType.Write,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xA0
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 3 },
                new Instruction() { name = "TAY", operation = TAY, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "TAX", operation = TAX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "BCS", operation = BCS, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0xB0
                new Instruction() { name = "LDA", operation = LDA, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ZPY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "CLV", operation = CLV, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "TSX", operation = TSX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "LDY", operation = LDY, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDA", operation = LDA, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "LDX", operation = LDX, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 4 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xC0
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xC2
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "INY", operation = INY, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "DEX", operation = DEX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "CPY", operation = CPY, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 }, // 0xCF
                new Instruction() { name = "BNE", operation = BNE, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0xD4
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "CLD", operation = CLD, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0xDC
                new Instruction() { name = "CMP", operation = CMP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "DEC", operation = DEC, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "CPX", operation = CPX, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xE0
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IZX, instr_type = CPUInstructionType.Read,    cycles = 6 },
                new Instruction() { name = "???", operation = NOP, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xE2
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "CPX", operation = CPX, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ZP0, instr_type = CPUInstructionType.Read,    cycles = 3 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ZP0, instr_type = CPUInstructionType.R_M_W,   cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 5 },
                new Instruction() { name = "INX", operation = INX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = SBC, addr_mode = IMM, instr_type = CPUInstructionType.Read,    cycles = 2 }, // 0xEB
                new Instruction() { name = "CPX", operation = CPX, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABS, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ABS, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "BEQ", operation = BEQ, addr_mode = REL, instr_type = CPUInstructionType.Branch,  cycles = 2 }, // 0xF0
                new Instruction() { name = "SBC", operation = SBC, addr_mode = IZY, instr_type = CPUInstructionType.Read,    cycles = 5 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 8 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0xF4
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ZPX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ZPX, instr_type = CPUInstructionType.R_M_W,   cycles = 6 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 6 },
                new Instruction() { name = "SED", operation = SED, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 2 },
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABY, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "NOP", operation = NOP, addr_mode = IMP, instr_type = CPUInstructionType.Read,    cycles = 2 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 },
                new Instruction() { name = "???", operation = NOP, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 }, // 0xFC
                new Instruction() { name = "SBC", operation = SBC, addr_mode = ABX, instr_type = CPUInstructionType.Read,    cycles = 4 },
                new Instruction() { name = "INC", operation = INC, addr_mode = ABX, instr_type = CPUInstructionType.R_M_W,   cycles = 7 },
                new Instruction() { name = "???", operation = XXX, addr_mode = IMP, instr_type = CPUInstructionType.Special, cycles = 7 }
            };
        }

        private void push(byte data)
        {
            write((ushort)(ADDR_STACK + sp), data);
            sp--;
        }

        private byte pop()
        {
            sp++;
            byte data = read((ushort)(ADDR_STACK + sp));
            return data;
        }

        private void testAndSet(FLAGS6502 flag, ushort data)
        {
            switch (flag)
            {
                case FLAGS6502.B:
                case FLAGS6502.C:
                case FLAGS6502.D:
                case FLAGS6502.U:
                case FLAGS6502.V:
                    break;
                case FLAGS6502.N:
                    setFlag(flag, (data & 0x0080) != 0);
                    break;
                case FLAGS6502.Z:
                    setFlag(flag, (data & 0x00FF) == 0);
                    break;
            }
        }

#if LOGMODE
        // private Log log;
#endif
    }
}
