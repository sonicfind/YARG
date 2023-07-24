﻿using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan.Instrument.ProGuitar
{
    public class Midi_ProGuitar22_Scanner : Midi_ProGuitar_Scanner_Base
    {
        public override bool ParseLaneColor()
        {
            uint noteValue = note.value - 24;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                uint lane = LANEVALUES[noteValue];
                if (lane < 6 && currEvent.channel != 1 && 100 <= note.velocity && note.velocity <= 122)
                    notes[diffIndex, lane] = true;
            }
            return false;
        }
    }
}
