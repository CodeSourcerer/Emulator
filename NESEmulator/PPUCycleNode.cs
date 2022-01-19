using System;
using System.Collections.Generic;

namespace NESEmulator
{
    public class PPUCycleNode
    {
        public short CycleStart { get; private set; }

        public List<Action> CycleOperations { get; private set; }

        public PPUCycleNode(short cycleStart, List<Action> cycleOps)
        {
            this.CycleStart = cycleStart;
            this.CycleOperations = cycleOps;
        }
    }
}
