using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator.APU
{
    public class APUVolumeEnvelope
    {
        public bool Start { get; set; }
        public bool ConstantVolume { get; set; }
        public bool EnvelopeLoop { get; set; }

        private byte _volume;
        public byte Volume
        {
            get
            {
                return ConstantVolume ? _volume : _decayLevel;
            }
            set
            {
                _volume = value;
                if (!ConstantVolume)
                    _divider.CounterReload = _volume + 1;
            }
        }

        private APUDivider _divider;
        private byte _decayLevel;

        public APUVolumeEnvelope()
        {
            _divider = new APUDivider(APUDivider.DividerType.COUNTDOWN, OnDividerReachedZero);
            //_divider.DividerReachedZero += OnDividerReachedZero;
        }

        // Called on quarter frames
        public void Clock()
        {
            //if (!ConstantVolume)
            //{
                if (!Start)
                {
                    _divider.Clock();
                }
                else
                {
                    Start = false;
                    _decayLevel = 15;
                    _divider.CounterReload = _volume;
                }
            //}
        }

        private void OnDividerReachedZero(object sender, EventArgs e)
        {
            if (_decayLevel > 0)
            {
                --_decayLevel;
            }
            else
            {
                if (EnvelopeLoop)
                    _decayLevel = 15;
            }
        }
    }
}
