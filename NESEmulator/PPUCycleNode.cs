using System;
namespace NESEmulator
{
    public class PPUCycleNode
    {
        public short CycleStart { get; private set; }
        public ushort CycleCount { get; private set; }

        public Action CycleOperation { get; private set; }

        public PPUCycleNode(short cycleStart, ushort cycleCount, Action cycleOp)
        {
            this.CycleStart = cycleStart;
            this.CycleCount = cycleCount;
            this.CycleOperation = cycleOp;
        }
    }
}
