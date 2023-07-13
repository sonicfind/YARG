using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace YARG.Song.Entries.TrackScan.Instrument.Drums
{
    public class Midi_Drum4Pro_Scanner : Midi_Drum_Scanner_Base
    {
        private bool _cymbals;
        public bool Cymbals => _cymbals;
        public Midi_Drum4Pro_Scanner(bool forceProDrums)
        {
            _cymbals = forceProDrums;
        }

        protected override bool IsNote() { return 60 <= note.value && note.value <= 100; }

        protected override bool IsFullyScanned() { return validations == 31 && _cymbals; }

        protected override bool ParseLaneColor()
        {
            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 60 || 100 < note.value)
                return false;

            uint noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6)
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        protected override bool ToggleExtraValues()
        {
            if (110 <= note.value && note.value <= 112)
            {
                _cymbals = true;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
