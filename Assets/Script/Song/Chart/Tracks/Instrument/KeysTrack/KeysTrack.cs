using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart.KeysTrack
{
    public class Midi_Keys_Loader : Midi_Instrument_Loader<Keys>
    {
        private readonly long[,] notes = new long[4, 5] {
            { -1, -1, -1, -1, -1 },
            { -1, -1, -1, -1, -1 },
            { -1, -1, -1, -1, -1 },
            { -1, -1, -1, -1, -1 },
        };

        private readonly int[] lanes = new int[] {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        static Midi_Keys_Loader() { }

        public Midi_Keys_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        protected override void ParseLaneColor(ref InstrumentTrack<Keys> track)
        {
            int noteValue = note.value - 60;
            int lane = lanes[noteValue];
            if (lane < 5)
            {
                int diffIndex = DIFFVALUES[noteValue];
                notes[diffIndex, lane] = currEvent.position;
                if (!track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Add_NoReturn(currEvent.position);
            }
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<Keys> track)
        {
            int noteValue = note.value - 60;
            int lane = lanes[noteValue];
            if (lane < 5)
            {
                int diffIndex = DIFFVALUES[noteValue];
                long colorPosition = notes[diffIndex, lane];
                if (colorPosition != -1)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[lane] = currEvent.position - colorPosition;
                    notes[diffIndex, lane] = -1;
                }
            }
        }
    }
}
