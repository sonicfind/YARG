using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public class Drum_4Pro : DrumNote_Pro, IReadableFromDotChart
    {
        public Drum_4Pro() : base(4) { }
        public Drum_4Pro(Drum_Legacy drum) : base(4, drum) { }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane == 0) _bass = length;
            else if (lane <= 4) pads[lane - 1].Duration = length;
            else if (lane == 32)
            {
                _doubleBass = _bass;
                _bass.Disable();
            }
            else if (66 <= lane && lane <= 68) cymbals[lane - 66] = true;
            else if (34 <= lane && lane <= 37) pads[lane - 34].Dynamics = DrumDynamics.Accent;
            else if (40 <= lane && lane <= 43) pads[lane - 40].Dynamics = DrumDynamics.Ghost;
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
