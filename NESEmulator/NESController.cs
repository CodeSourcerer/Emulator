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
            SELECT      = 0x20,
            START       = 0x10,
            UP          = 0x08,
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

        public byte[] ControllerState { get; private set; }
        private byte[] _controller_state;

        public NESController()
        {
            ControllerState = new byte[2];
            _controller_state = new byte[2];
        }

        public void Clock(ulong clockCounter)
        {
            
        }

        public bool Read(ushort addr, out byte data, bool readOnly = false)
        {
            bool dataRead = false;
            data = 0;

            if (addr == 0x4016 || addr == 0x4017)
            {
                int controllerNum = addr & 0x0001;
                // We OR with 0x40 to support the "open bus behavior" with the controller, required for Paper Boy
                data = (byte)(0x40 | ((_controller_state[controllerNum] & 0x80) > 0 ? 1 : 0));
                if (!readOnly)
                    _controller_state[controllerNum] <<= 1;
                // _controller_state[controllerNum] |= 0x01;
                dataRead = true;
            }

            return dataRead;
        }

        public bool Write(ushort addr, byte data)
        {
            bool dataWritten = false;

            if (addr == 0x4016 || addr == 0x4017)
            {
                int controllerNum = addr & 0x0001;
                _controller_state[controllerNum] = ControllerState[controllerNum];
                //dataWritten = true;
            }

            return dataWritten;
        }

        public void Reset()
        {
            ControllerState[0] = (byte)NESButton.NO_PRESS;
            ControllerState[1] = (byte)NESButton.NO_PRESS;
        }

        public void PowerOn()
        {
            ControllerState[0] = (byte)NESButton.NO_PRESS;
            ControllerState[1] = (byte)NESButton.NO_PRESS;
        }

        public void Press(Controller controller, NESButton button)
        {
            ControllerState[(int)controller] |= (byte)button;
        }
    }
}
