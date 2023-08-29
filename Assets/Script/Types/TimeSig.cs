using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Types
{
    public struct TimeSig
    {
        public byte Numerator;
        public byte Denominator;
        public byte Metronome;
        public byte Num32nds;

        public TimeSig(byte numerator, byte denominator, byte metronome, byte num32nds)
        {
            Numerator = numerator;
            Denominator = denominator;
            Metronome = metronome;
            Num32nds = num32nds;
        }
    };
}
