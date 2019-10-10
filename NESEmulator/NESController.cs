using System;
using NESEmulator.Util;

namespace NESEmulator
{
    public class NESController : BusDevice
    {
        public BusDeviceType DeviceType { get { return BusDeviceType.CONTROLLER; } }

        [Flags]
        public enum NESButton
        {
            A           = 0x80,
            B           = 0x40,
            UP          = 0x20,
            SELECT      = 0x10,
            START       = 0x08,
            DOWN        = 0x04,
            LEFT        = 0x02,
            RIGHT       = 0x01,
            NO_PRESS    = 0x00
        }

        public enum Controller
        {
            Controller1 = 0,
            Controller2 = 1
        }

        public NESButton[] ControllerState { get; private set; }
        private NESButton[] _controller_state;

        public NESController()
        {
            ControllerState = new NESButton[2];
            _controller_state = new NESButton[2];
        }

        public void Clock(ulong clockCounter)
        {
            
        }

        public bool Read(ushort addr, out byte data)
        {
            bool dataRead = false;
            data = 0;

            if (addr >= 0x4016 && addr <= 0x4017)
            {
                int controllerNum = addr & 0x0001;
                data = (byte)(_controller_state[controllerNum].HasFlag(NESButton.A) ? 1 : 0);
                _controller_state[controllerNum] = (NESButton)((int)_controller_state[controllerNum] << 1);
                dataRead = true;
            }

            return dataRead;
        }

        public bool Write(ushort addr, byte data)
        {
            bool dataWritten = false;

            if (addr >= 0x4016 && addr <= 0x4017)
            {
                _controller_state[addr & 0x0001] = ControllerState[addr & 0x0001];
                dataWritten = true;
            }

            return dataWritten;
        }

        public void Reset()
        {
            ControllerState[0] = NESButton.NO_PRESS;
            ControllerState[1] = NESButton.NO_PRESS;
        }

        public void Press(Controller controller, NESButton button)
        {
            ControllerState[(int)controller] |= button;
        }
    }
}
