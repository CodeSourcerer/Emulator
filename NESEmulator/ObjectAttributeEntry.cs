using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    /// <summary>
    /// NES Sprite structure
    /// </summary>
    public struct ObjectAttributeEntry
    {
        public byte y;          // Y position of sprite
        public byte id;         // ID of tile from pattern memory
        public byte attribute;  // Flags that define how sprite should be rendered
        public byte x;          // X position of sprite

        public byte this[int i]
        {
            get
            {
                byte val = 0;
                //i %= 4;
                switch (i)
                {
                    case 0:
                        val = y;
                        break;
                    case 1:
                        val = id;
                        break;
                    case 2:
                        val = attribute;
                        break;
                    case 3:
                        val = x;
                        break;
                    default:
                        throw new ApplicationException("Eeeek!");
                }

                return val;
            }

            set
            {
                //i %= 4;
                switch (i)
                {
                    case 0:
                        y = value;
                        break;
                    case 1:
                        id = value;
                        break;
                    case 2:
                        attribute = value;
                        break;
                    case 3:
                        x = value;
                        break;
                    default:
                        throw new ApplicationException("Eeeek!");
                }
            }
        }

        public void Fill(byte val)
        {
            y = id = attribute = x = val;
        }
    }
}
