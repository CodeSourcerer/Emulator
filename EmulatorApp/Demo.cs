using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using csPixelGameEngine;
using CS6502;
using System.IO;
using csPixelGameEngine.enums;
using OpenTK;
using OpenTK.Input;

namespace EmulatorApp
{
    class Demo
    {
        private const int SCREEN_WIDTH = 680;
        private const int SCREEN_HEIGHT = 480;

        private PixelGameEngine pge;
        private GLWindow window;
        private Bus nes;
        private CS6502.CS6502 cpu;
        private Dictionary<ushort, string> mapAsm;

        public Demo(string appName)
        {
            window = new GLWindow(SCREEN_WIDTH, SCREEN_HEIGHT, 2, 2, appName);
            window.KeyDown += Window_KeyDown;
            pge = new PixelGameEngine(appName);
            pge.Construct(SCREEN_WIDTH, SCREEN_HEIGHT, window);
            pge.OnCreate += pge_OnCreate;
            pge.OnFrameUpdate += pge_OnUpdate;
            nes = new Bus(0xFFFF);
            cpu = new CS6502.CS6502();
            cpu.ConnectBus(nes);
        }

        public void Start()
        {
            pge.Start();
        }

        private void Window_KeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if (e.IsRepeat)
                return;

            switch(e.Key)
            {
                case OpenTK.Input.Key.Space:
                    do
                    {
                        cpu.Clock();
                    } while (!cpu.isComplete());
                    break;

                case OpenTK.Input.Key.R:
                    cpu.Reset();
                    break;

                case OpenTK.Input.Key.I:
                    cpu.IRQ();
                    break;

                case OpenTK.Input.Key.N:
                    cpu.NMI();
                    break;
            }
        }

        private void pge_OnUpdate(object sender, FrameUpdateEventArgs frameUpdateArgs)
        {
            pge.Clear(Pixel.BLUE);

            // Draw Ram Page 0x00		
            DrawRam(2, 2, 0x0000, 16, 16);
            DrawRam(2, 182, 0x8000, 16, 16);
            DrawCpu(448, 2);
            DrawCode(448, 72, 26);


            pge.DrawString(10, 370, "SPACE = Step Instruction    R = RESET    I = IRQ    N = NMI", Pixel.WHITE);
        }

        private void pge_OnCreate(object sender, EventArgs e)
        {
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
            mapAsm = cpu.Disassemble(0x0000, 0xFFFF);

            cpu.Reset();

            pge.Clear(Pixel.BLUE);
        }

        static void Main(string[] args)
        {
            Demo demo = new Demo("NES Emulator");
            demo.Start();
        }

        void DrawRam(int x, int y, ushort nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                string sOffset = string.Format("${0}:", nAddr.ToString("X4"));
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += string.Format(" {0}", nes.Read(nAddr, true).ToString("X2"));
                    nAddr += 1;
                }
                pge.DrawString(nRamX, nRamY, sOffset, Pixel.WHITE);
                nRamY += 10;
            }
        }

        void DrawCpu(int x, int y)
        {
            pge.DrawString(x, y, "STATUS:", Pixel.WHITE);
            pge.DrawString(x + 64, y, "N", cpu.status.HasFlag(FLAGS6502.N) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 80, y, "V", cpu.status.HasFlag(FLAGS6502.V) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 96, y, "-", cpu.status.HasFlag(FLAGS6502.U) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 112, y, "B", cpu.status.HasFlag(FLAGS6502.B) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 128, y, "D", cpu.status.HasFlag(FLAGS6502.D) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 144, y, "I", cpu.status.HasFlag(FLAGS6502.I) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 160, y, "Z", cpu.status.HasFlag(FLAGS6502.Z) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 178, y, "C", cpu.status.HasFlag(FLAGS6502.C) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x, y + 10, string.Format("PC: ${0}", cpu.pc.ToString("X4")), Pixel.WHITE);
            pge.DrawString(x, y + 20, string.Format("A: ${0} [{1}]", cpu.a.ToString("X2"), cpu.a.ToString()), Pixel.WHITE);
            pge.DrawString(x, y + 30, string.Format("X: ${0} [{1}]", cpu.x.ToString("X2"), cpu.x.ToString()), Pixel.WHITE);
            pge.DrawString(x, y + 40, string.Format("Y: ${0} [{1}]", cpu.y.ToString("X2"), cpu.y.ToString()), Pixel.WHITE);
            pge.DrawString(x, y + 50, string.Format("Stack P: ${0}", cpu.sp.ToString("X4")), Pixel.WHITE);
        }

        void DrawCode(int x, int y, int nLines)
        {
            ushort pc = cpu.pc;
            int nLineY = (nLines >> 1) * 10 + y;
            ushort lastMem = mapAsm.Last().Key;
            var memKeysItr = mapAsm.Keys.GetEnumerator();
            LinkedList<ushort> keyList = new LinkedList<ushort>(mapAsm.Keys);

            var pcItr = keyList.Find(pc);

            if (pcItr != null && pcItr.Value != lastMem)
            {
                pge.DrawString(x, nLineY, mapAsm[pcItr.Value], Pixel.CYAN);
                while (nLineY < (nLines * 10) + y)
                {
                    nLineY += 10;
                    pcItr = pcItr.Next;
                    if (pcItr != null && pcItr.Value != lastMem)
                    {
                        pge.DrawString(x, nLineY, mapAsm[pcItr.Value], Pixel.CYAN);
                    }
                    else
                        break;
                }
            }

            pcItr = keyList.Find(pc);

            nLineY = (nLines >> 1) * 10 + y;
            if (pcItr != null && pcItr.Value != lastMem)
            {
                while (nLineY > y)
                {
                    nLineY -= 10;
                    pcItr = pcItr.Previous;
                    if (pcItr != null && pcItr.Value != lastMem)
                    {
                        pge.DrawString(x, nLineY, mapAsm[pcItr.Value], Pixel.CYAN);
                    }
                    else
                        break;
                }
            }
        }
    }
}
