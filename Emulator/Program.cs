using System;
using System.IO;
using CS6502;

namespace Emulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            CS6502.CS6502 cpu = new CS6502.CS6502();
            Bus nes = new Bus(0xFFFF);
            cpu.ConnectBus(nes);

            loadROM(nes, "./tests/1.Branch_Basics.nes");

            do
            {
                cpu.Clock();
            }
            while (true);
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
