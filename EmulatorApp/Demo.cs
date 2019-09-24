using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using csPixelGameEngine;
using CS6502;
using System.IO;
using csPixelGameEngine.enums;

namespace EmulatorApp
{
    class Demo : PixelGameEngine
    {
        private Bus nes;
        private CS6502.CS6502 cpu;
        private Dictionary<ushort, string> mapAsm;

        public Demo(string appName)
            : base(appName)
        {
            nes = new Bus(0xFFFF);
            cpu = new CS6502.CS6502();
            cpu.ConnectBus(nes);
        }

        static void Main(string[] args)
        {
            Demo demo = new Demo("Demo");
        }

        public override bool OnUserCreate()
        {
            string programStr = "A2 0A 8E 00 00 A2 03 8E 01 00 AC 00 00 A9 00 18 6D 01 00 88 D0 FA 8D 02 00 EA EA EA";
            ushort offset = 0x8000;

            string[] prog = programStr.Split(" ".ToCharArray());

            foreach(string instByte in prog)
            {
                nes.Write(offset, Convert.ToByte(instByte, 16));
                offset++;
            }

            // Set reset vector
            nes.Write(CS6502.CS6502.ADDR_PC, 0x00);
            nes.Write(CS6502.CS6502.ADDR_PC + 1, 0x80);

            // Extract disassembly
            mapAsm = cpu.Disassemble(0x0000, 0xFFFF);

            return true;
        }

        public override bool OnUserUpdate(float elapsedTime)
        {
            Clear(Pixel.BLUE);


            if (GetKey(Key.SPACE).pressed)
            {
                do
                {
                    cpu.Clock();
                }
                while (!cpu.isComplete());
            }

            if (GetKey(Key.R).pressed)
                cpu.Reset();

            if (GetKey(Key.I).pressed)
                cpu.IRQ();

            if (GetKey(Key.N).pressed)
                cpu.NMI();

            // Draw Ram Page 0x00		
            DrawRam(2, 2, 0x0000, 16, 16);
            DrawRam(2, 182, 0x8000, 16, 16);
            //DrawCpu(448, 2);
            //DrawCode(448, 72, 26);


            DrawString(10, 370, "SPACE = Step Instruction    R = RESET    I = IRQ    N = NMI");

            return true;
        }

        void DrawRam(int x, int y, ushort nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                string sOffset = string.Format("${0}:", nAddr.ToString("X4"));
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += string.Format(" ", nes.Read(nAddr, true).ToString("X2"));
                    nAddr += 1;
                }
                DrawString(nRamX, nRamY, sOffset);
                nRamY += 10;
            }
        }
    }
}
