using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan.Instrument.ProKeys
{
    public class Midi_ProKeys_Scanner : Midi_Instrument_Scanner_Base
    {
        private readonly bool[] lanes = new bool[25];
        public readonly int difficulty;
        public Midi_ProKeys_Scanner(int difficulty) { this.difficulty = difficulty; }

        protected override bool IsNote() { return 48 <= note.value && note.value <= 72; }

        protected override bool ParseLaneColor()
        {
            lanes[note.value - 48] = true;
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 48 || 72 < note.value || !lanes[note.value - 48])
                return false;

            Validate(difficulty);
            return true;
        }
    }
}
