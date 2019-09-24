using System;
using System.Collections.Generic;
using System.IO;
using CS6502;

namespace Emulator
{
    class Program
    {
        static void Main(string[] args)
        {
            CS6502.CS6502 cpu = new CS6502.CS6502();
            Bus nes = new Bus(0xFFFF);
            cpu.ConnectBus(nes);

            string programStr = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";
            ushort offset = 0x8000;

            string[] prog = programStr.Split(" ".ToCharArray());

            foreach (string instByte in prog)
            {
                nes.Write(offset, Convert.ToByte(instByte, 16));
                offset++;
            }

            // Set reset vector
            nes.Write(CS6502.CS6502.ADDR_PC, 0x00);
            nes.Write(CS6502.CS6502.ADDR_PC + 1, 0x80);

            // Extract disassembly
            Dictionary<ushort, string> mapAsm = cpu.Disassemble(0x8000, (ushort)(0x8000 + prog.Length));

            foreach(var m in mapAsm)
            {
                Console.WriteLine(m.Value);
            }
        }

        private static void loadROM(Bus nes, string filename)
        {
            FileStream fs = File.OpenRead(filename);
            byte[] data = new byte[fs.Length];
            ReadWholeArray(fs, data);

            nes.LoadROM(data, 0x2010);
        }

        public static void ReadWholeArray(Stream stream, byte[] data)
        {
            int offset = 0;
            int remaining = data.Length;
            while (remaining > 0)
            {
                int read = stream.Read(data, offset, remaining);
                if (read <= 0)
                    throw new EndOfStreamException
                        (String.Format("End of stream reached with {0} bytes left to read", remaining));
                remaining -= read;
                offset += read;
            }
        }
    }
}
