using System;
using System.Collections.Generic;
using System.Text;

namespace NESEmulator
{
    public interface IScanlineCounterMapper
    {
        void OnSpriteFetch(object sender, EventArgs e);
    }
}
