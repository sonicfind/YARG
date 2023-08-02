using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public class Drum_Legacy : DrumNote_Pro, IReadableFromDotChart
    {
        public override int NumLanes => 5;
        public Drum_Legacy() : base(5) { }

        public bool Set_From_Chart(uint lane, long length)
        {
            if (lane == 0) _bass = length;
            else if (lane <= 5) pads[lane - 1].Duration = length;
            else if (lane == 32)
            {
                _doubleBass = _bass;
                _bass.Disable();
            }
            else if (66 <= lane && lane <= 68) cymbals[lane - 66] = true;
            else if (34 <= lane && lane <= 38) pads[lane - 34].Dynamics = DrumDynamics.Accent;
            else if (40 <= lane && lane <= 44) pads[lane - 40].Dynamics = DrumDynamics.Ghost;
            else
                return false;
            return true;
        }

        public DrumType ParseDrumType()
        {
            if (pads[4].IsActive())
                return DrumType.FIVE_LANE;

            for (int i = 0; i < 3; ++i)
                if (cymbals[i])
                    return DrumType.FOUR_PRO;
            return DrumType.UNKNOWN;
        }

        public static DrumType EvaluateDrumType(uint index)
        {
            if (index == 5)
                return DrumType.FIVE_LANE;
            else if (66 <= index && index <= 68)
                return DrumType.FOUR_PRO;
            else
                return DrumType.UNKNOWN;
        }

#nullable enable
        public override PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }
}
