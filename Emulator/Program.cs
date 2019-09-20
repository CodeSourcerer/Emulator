using System;
using CS6502;

namespace Emulator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            CS6502.CS6502 cpu = new CS6502.CS6502();
            IBus nes = new Bus(cpu);
            cpu.ConnectBus(nes);
        }
    }
}
