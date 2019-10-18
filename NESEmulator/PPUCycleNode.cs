using System;
namespace NESEmulator
{
    public class PPUCycleNode
    {
        public short CycleStart { get; private set; }

        public Action CycleOperation { get; private set; }

        public PPUCycleNode(short cycleStart, Action cycleOp)
        {
            this.CycleStart = cycleStart;
            this.CycleOperation = cycleOp;
        }
    }
}
