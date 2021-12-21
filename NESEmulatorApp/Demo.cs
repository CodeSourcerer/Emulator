using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using csPixelGameEngineCore;
using System.Threading;
using log4net;
using log4net.Config;
using NESEmulator;
using OpenTK;
using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
//using OpenTK.Input;
using Microsoft.Extensions.DependencyInjection;
using OpenTK.Graphics;
using csPixelGameEngineCore.Enums;

namespace NESEmulatorApp
{
    class Demo
    {
        private const int SCREEN_WIDTH      = 700;
        private const int SCREEN_HEIGHT     = 300;
        private const int NUM_AUDIO_BUFFERS = 50;
        private const string APP_NAME = "NES Emulator";

        private static ILog Log = LogManager.GetLogger(typeof(Demo));
        private static ServiceProvider serviceProvider;

        private PixelGameEngine pge;
        private GLWindow window;
        private NESBus nesBus;
        private bool runEmulation;

        // Debugging
        private Dictionary<ushort, string> mapAsm;
        private int selectedPalette;

        // Audio vars
        private AudioContext audioContext;
        private int[] buffers, sources;
        private Stack<int> _availableBuffers;

        // Update vars
        private int _frameCount;
        private int _fps;
        private DateTime dtStart = DateTime.Now;
        private double residualTime = 0.0;

        public Demo(string appName)
        {
            _availableBuffers = new Stack<int>(NUM_AUDIO_BUFFERS);
            initAudioStuff();
            //window = new GLWindow(SCREEN_WIDTH, SCREEN_HEIGHT, 3, 3, appName);
            //window.KeyDown += Window_KeyDown;
            var renderer = serviceProvider.GetService<IRenderer>();
            pge = new PixelGameEngine(renderer, serviceProvider.GetService<IPlatform>(), appName);
            pge.Construct(SCREEN_WIDTH, SCREEN_HEIGHT, 2, 2, false, false);
            pge.OnCreate += pge_OnCreate;
            pge.OnFrameUpdate = pge_OnUpdate;
            pge.OnDestroy += pge_OnDestroy;
            pge.Platform.KeyDown += OnKeyDown;
        }

        static void Main(string[] args)
        {
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            Thread.CurrentThread.Name = "main";
            Log.Info($"NES app started with args {string.Join(' ', args)}");

            var gameWindow = new GameWindow(SCREEN_WIDTH, SCREEN_HEIGHT, GraphicsMode.Default, APP_NAME, GameWindowFlags.Default, DisplayDevice.Default, 2, 1,
                                GraphicsContextFlags.Default);

            serviceProvider = new ServiceCollection()
                .AddSingleton(gameWindow)
                .AddScoped<IRenderer, GL21Renderer>()
                .AddScoped<IPlatform, OpenTkPlatform>()
                .BuildServiceProvider();

            Demo demo = new Demo(APP_NAME);

            //string romfile = "tests\\smb_2.nes";
            //string romfile = "tests\\smb2.nes";
            //string romfile = "tests\\smb3.nes";
            //string romfile = "tests\\capt_america.nes";
            //string romfile = "tests\\Abadox.nes";
            //string romfile = "tests\\burgertime.nes";
            //string romfile = "tests\\ice_climber.nes";
            //string romfile = "tests\\pacman-namco.nes";
            //string romfile = "tests\\ducktales.nes";
            //string romfile = "tests\\young_indy.nes";
            // Mapper 1
            //string romfile = "tests\\zelda.nes";
            //string romfile = "tests\\zeldaII.nes";
            //string romfile = "tests\\megaman2.nes";

            // Test roms
            //string romfile = "tests\\nestest.nes";
            //string romfile = "tests\\instr_test_v5\\official_only.nes"; // passes
            //string romfile = "tests\\1.Branch_Basics.nes";
            //string romfile = "tests\\2.Backward_Branch.nes";
            //string romfile = "tests\\3.Forward_Branch.nes";
            //string romfile = "tests\\instr_timing\\1-instr_timing.nes"; // passes except for illegal opcodes
            //string romfile = "tests\\instr_timing\\2-branch_timing.nes"; // passes
            //string romfile = "tests\\instr_misc\\01-abs_x_wrap.nes"; // passes
            //string romfile = "tests\\instr_misc\\02-branch_wrap.nes"; // passes
            //string romfile = "tests\\instr_misc\\03-dummy_reads.nes"; // passes
            //string romfile = "tests\\instr_misc\\04-dummy_reads_apu.nes";
            //string romfile = "tests\\ppu_vbl_nmi.nes"; // displays white screen
            //string romfile = "tests\\ppu_vbl_nmi\\01-vbl_basics.nes"; // failed #7 - vbl period too short
            //string romfile = "tests\\apu_test.nes";
            // APU tests
            //string romfile = "tests\\APU\\01.len_ctr.nes"; // passes
            //string romfile = "tests\\APU\\02.len_table.nes"; // passes
            //string romfile = "tests\\APU\\03.irq_flag.nes"; // passes
            //string romfile = "tests\\APU\\04.clock_jitter.nes"; // passes
            //string romfile = "tests\\APU\\05.len_timing_mode0.nes"; // passes
            //string romfile = "tests\\APU\\06.len_timing_mode1.nes"; // passes
            //string romfile = "tests\\APU\\07.irq_flag_timing.nes"; // passes
            //string romfile = "tests\\APU\\08.irq_timing.nes"; // fails - $03 -- too late"
            //string romfile = "tests\\APU\\10.len_halt_timing.nes"; // fails - "3) Length should be clocked when halted at 14915"
            //string romfile = "tests\\APU\\11.len_reload_timing.nes"; // fails - "4) Reload during length clock when ctr = 0 should work normally"
            // From mmc3_test_2
            //string romfile = "tests\\mmc3_test_2\\1-clocking.nes";
            // From cpu_interrupts_v2
            //string romfile = "tests\\cpu_interrupts.nes"; // white screen
            //string romfile = "tests\\cpu_interrupts_v2\\1-cli_latency.nes"; // passes
            string romfile = "tests\\cpu_interrupts_v2\\2-nmi_and_brk.nes"; // fails
            //string romfile = "tests\\cpu_interrupts_v2\\3-nmi_and_irq.nes";
            //string romfile = "tests\\cpu_interrupts_v2\\4-irq_and_dma.nes";
            //string romfile = "tests\\cpu_interrupts_v2\\5-branch_delays_irq.nes";
            //string romfile = "tests\\ppu_vbl_nmi\\01-vbl_basics.nes";

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                romfile = args[0];

            Cartridge cartridge = demo.LoadCartridge(romfile);

            demo.Start(cartridge);
        }

        public void Start(Cartridge cartridge)
        {
            nesBus = new NESBus();

            if (!cartridge.ImageValid)
                throw new ApplicationException("Invalid ROM image");
            nesBus.InsertCartridge(cartridge);
            nesBus.Reset();

            pge.Start();
        }

        private void initAudioStuff()
        {
            audioContext = new AudioContext();
            buffers = AL.GenBuffers(NUM_AUDIO_BUFFERS);
            sources = AL.GenSources(1);
            foreach (int buf in buffers)
                _availableBuffers.Push(buf);
        }

        public Cartridge LoadCartridge(string fileName)
        {
            Cartridge cartridge = new Cartridge(nesBus);

            using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read))
            {
                using (BinaryReader br = new BinaryReader(fs))
                {
                    cartridge.ReadCartridge(br);
                }
            }

            return cartridge;
        }

        private void handleControllerInputs(KeyboardState keyState)
        {
            nesBus.Controller.Reset();

            if (keyState.IsKeyDown(Key.X))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.A);

            if (keyState.IsKeyDown(Key.Z))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.B);

            if (keyState.IsKeyDown(Key.A))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.START);

            if (keyState.IsKeyDown(Key.S))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.SELECT);

            if (keyState.IsKeyDown(Key.Up))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.UP);
            else if (keyState.IsKeyDown(Key.Down))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.DOWN);
            else if (keyState.IsKeyDown(Key.Left))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.LEFT);
            else if (keyState.IsKeyDown(Key.Right))
                nesBus.Controller.Press(NESController.Controller.Controller1, NESController.NESButton.RIGHT);
        }

        private void OnKeyDown(object sender, KeyboardEventArgs e)
        {
            if (e.IsRepeat)
                return;

            // Emulator inputs
            switch (e.Key)
            {
                case Key.Space:
                    runEmulation = !runEmulation;
                    break;

                case Key.R:
                    nesBus.Reset();
                    break;

                case Key.P:
                    selectedPalette = (selectedPalette + 1) & 0x07;
                    break;

                case Key.F:
                    do
                    {
                        nesBus.clock();
                    } while (!nesBus.PPU.FrameComplete);
                    do
                    {
                        nesBus.clock();
                    } while (!nesBus.CPU.isComplete());
                    nesBus.PPU.FrameComplete = false;
                    break;

                case Key.C:
                    do
                    {
                        nesBus.clock();
                    } while (!nesBus.CPU.isComplete());
                    do
                    {
                        nesBus.clock();
                    } while (nesBus.CPU.isComplete());
                    break;
            }
        }

        private void pge_OnUpdate(object sender, FrameUpdateEventArgs frameUpdateArgs)
        {
            pge.Clear(Pixel.BLUE);

            if (runEmulation)
            {
                if (residualTime > 0.0f)
                    residualTime -= frameUpdateArgs.ElapsedTime;
                else
                {
                    residualTime += (1.0 / 60.0988) - frameUpdateArgs.ElapsedTime;

                    handleControllerInputs(pge.Platform.KeyboardState);
                    do
                    {
                        nesBus.clock();
                        playAudioWhenReady();
                    } while (!nesBus.PPU.FrameComplete);
                    nesBus.PPU.FrameComplete = false;
                    _frameCount++;

                    if (DateTime.Now - dtStart >= TimeSpan.FromSeconds(1))
                    {
                        dtStart = DateTime.Now;
                        _fps = _frameCount;
                        _frameCount = 0;
                    }
                }
            }

            pge.DrawString(280, 2, $"FPS: {_fps}", Pixel.WHITE);

            // Draw rendered output
            pge.DrawSprite(0, 0, nesBus.PPU.GetScreen(), 1);

            // Draw Ram Page 0x00
            //DrawRam(2, 2, 0x0000, 16, 16);
            //DrawRam(2, 182, 0x0100, 16, 16);
            //DrawCpu(516, 2);
            //DrawCode(516, 72, 26);
            //DrawOam(270, 140, 0, 32);
            //DrawOam(500, 140, 32, 32);

            // Draw Palettes & Pattern Tables
            //DrawPalettes(516, 340);

            // Draw selection recticle around selected palette
            //pge.DrawRect(516 + selectedPalette * (swatchSize * 5) - 1, 339, (swatchSize * 4), swatchSize, Pixel.WHITE);

            // Generate Pattern Tables
            //pge.DrawSprite(316, 10, nesBus.PPU.GetPatternTable(0, (byte)selectedPalette));
            //pge.DrawSprite(448, 10, nesBus.PPU.GetPatternTable(1, (byte)selectedPalette));
        }

        private void pge_OnCreate(object sender, EventArgs e)
        {
            // Extract disassembly
            //mapAsm = cpu.Disassemble(0x0000, 0xFFFF);

            //cpu.Reset();

            pge.Clear(Pixel.BLUE);
        }

        private void pge_OnDestroy(object sender, EventArgs e)
        {
            AL.DeleteBuffers(buffers);
            AL.DeleteSources(sources);
            audioContext?.Dispose();
        }

        private void playAudioWhenReady()
        {
            // Get audio data
            if (nesBus.APU.IsAudioBufferReadyToPlay())
            {
                dequeueProcessedBuffers();

                var soundData = nesBus.APU.ReadAndResetAudio();
                if (_availableBuffers.Count > 0)
                {
                    int buffer = _availableBuffers.Pop();
                    AL.BufferData(buffer, ALFormat.Mono16, soundData, soundData.Length * 2, 44100);
                    AL.SourceQueueBuffer(sources[0], buffer);
                    if (AL.GetSourceState(sources[0]) != ALSourceState.Playing)
                    {
                        AL.SourcePlay(sources[0]);
                        //Log.Debug("Sound buffers emptied - restarted audio");
                    }
                }
                else
                {
                    Log.Debug("No available sound buffers!");
                }
            }
        }

        private void dequeueProcessedBuffers()
        {
            int processed;
            AL.GetSource(sources[0], ALGetSourcei.BuffersProcessed, out processed);
            if (processed > 0)
            {
                var buffersDequeued = AL.SourceUnqueueBuffers(sources[0], processed);
                foreach (var dqBuf in buffersDequeued)
                    _availableBuffers.Push(dqBuf);
            }
        }

        void DrawPalettes(int x, int y)
        {
            const int swatchSize = 6;
            for (int p = 0; p < 8; p++)
            {
                int ps5 = p * swatchSize * 5;
                for (int s = 0; s < 4; s++)
                {
                    pge.FillRect(x + ps5 + s * swatchSize, y, swatchSize, swatchSize, nesBus.PPU.GetColorFromPaletteRam((byte)p, (byte)s));
                }
            }
        }

        void DrawRam(int x, int y, ushort nAddr, int nRows, int nColumns)
        {
            int nRamX = x, nRamY = y;
            for (int row = 0; row < nRows; row++)
            {
                string sOffset = string.Format("${0}:", nAddr.ToString("X4"));
                for (int col = 0; col < nColumns; col++)
                {
                    sOffset += string.Format(" {0}", nesBus.Read(nAddr, true).ToString("X2"));
                    nAddr += 1;
                }
                pge.DrawString(nRamX, nRamY, sOffset, Pixel.WHITE);
                nRamY += 10;
            }
        }

        void DrawCpu(int x, int y)
        {
            pge.DrawString(x, y, "STATUS:", Pixel.WHITE);
            pge.DrawString(x + 64, y, "N", nesBus.CPU.status.HasFlag(FLAGS6502.N) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 80, y, "V", nesBus.CPU.status.HasFlag(FLAGS6502.V) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 96, y, "-", nesBus.CPU.status.HasFlag(FLAGS6502.U) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 112, y, "B", nesBus.CPU.status.HasFlag(FLAGS6502.B) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 128, y, "D", nesBus.CPU.status.HasFlag(FLAGS6502.D) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 144, y, "I", nesBus.CPU.status.HasFlag(FLAGS6502.I) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 160, y, "Z", nesBus.CPU.status.HasFlag(FLAGS6502.Z) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x + 178, y, "C", nesBus.CPU.status.HasFlag(FLAGS6502.C) ? Pixel.GREEN : Pixel.RED);
            pge.DrawString(x, y + 10, $"PC: ${nesBus.CPU.pc:X4}", Pixel.WHITE);
            pge.DrawString(x, y + 20, $"A: ${nesBus.CPU.a:X2} [{nesBus.CPU.a}]", Pixel.WHITE);
            pge.DrawString(x, y + 30, $"X: ${nesBus.CPU.x:X2} [{nesBus.CPU.x}]", Pixel.WHITE);
            pge.DrawString(x, y + 40, $"Y: ${nesBus.CPU.y:X2} [{nesBus.CPU.y}]", Pixel.WHITE);
            pge.DrawString(x, y + 50, $"Stack P: ${nesBus.CPU.sp:X4}", Pixel.WHITE);
        }

        void DrawCode(int x, int y, int nLines)
        {
            ushort pc = nesBus.CPU.pc;
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

        void DrawOam(int x, int y, int start, int count)
        {
            for (int i = start, y_offset = 0; i < start + count; i++, y_offset++)
            {
                string sOAM = $"{i:X2}: ({nesBus.PPU.OAM[i].x}, {nesBus.PPU.OAM[i].y}) ID: {nesBus.PPU.OAM[i].id:X2} AT: {nesBus.PPU.OAM[i].attribute:X2}";
                pge.DrawString(x, y + y_offset * 10, sOAM, Pixel.WHITE);
            }
        }
    }
}
