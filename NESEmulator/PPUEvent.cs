using System;
using System.Collections.Generic;
using System.Text;
using csPixelGameEngineCore;

namespace NESEmulator
{
    public enum PPUEventType
    {
        PPURegisterWrite,
        PPURegisterRead,
        SpriteZeroHit,
        IRQ
    }

    public struct PPUEvent
    {
        public PPUEventType EventType { get; set; }
        public ushort Address { get; set; }
        public byte Data { get; set; }
        public Pixel Color { get; set; }
        public int Scanline { get; set; }
        public int Cycle { get; set; }
    }
}
