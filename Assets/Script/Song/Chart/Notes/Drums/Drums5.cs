using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public class Drum_5 : DrumNote, IReadableFromDotChart
    {
        public Drum_5() : base(5) { }
        public Drum_5(Drum_Legacy drum) : base(5, drum) { }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane == 0) _bass = length;
            else if (lane <= 5) pads[lane - 1].Duration = length;
            else if (lane == 32)
            {
                _doubleBass = _bass;
                _bass.Disable();
            }
            else if (34 <= lane && lane <= 38) pads[lane - 34].Dynamics = DrumDynamics.Accent;
            else if (40 <= lane && lane <= 44) pads[lane - 40].Dynamics = DrumDynamics.Ghost;
            else
                return false;
            return true;
        }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }
}
