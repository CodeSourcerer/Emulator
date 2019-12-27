using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    /// <summary>
    /// This is a memory reader to be used by components external to the CPU when they need exclusive access to memory without
    /// the CPU interfering.
    /// </summary>
    public class MemoryReader
    {
        public ushort MemoryPtr { get; set; }
        public bool BufferReady { get; set; }
        public byte Buffer { get; set; }
        public byte CyclesToComplete { get; set; }

        public event EventHandler MemoryReadRequest;
        //public event EventHandler ReaderReady;

        public void BeginRead(object initiator)
        {
            MemoryReadRequest?.Invoke(initiator, EventArgs.Empty);
        }
    }
}
