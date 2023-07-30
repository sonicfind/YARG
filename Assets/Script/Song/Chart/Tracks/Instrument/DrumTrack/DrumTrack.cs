using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart.DrumTrack
{
    public abstract class Midi_Drum_Loader_Base<T> : Midi_Instrument_Loader<T>
        where T : DrumNote, new()
    {
        internal static readonly int[] LANEVALUES = new int[] {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };
        internal static readonly byte[] DYNAMICS_STRING = Encoding.ASCII.GetBytes("[ENABLE_CHART_DYNAMICS]");

        protected class Drum4_MidiDiff
        {
            public bool Flam { get; set; }
            public readonly long[] notes = new long[7] { -1, -1, -1, -1, -1, -1, -1 };
            public Drum4_MidiDiff() { }
        }

        protected readonly Drum4_MidiDiff[] difficulties = new Drum4_MidiDiff[4] { new(), new(), new(), new(), };
        protected bool enableDynamics = false;
        protected readonly bool[] toms = new bool[3];
        protected Midi_Drum_Loader_Base(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        protected override bool ProcessSpecialNote(ref InstrumentTrack<T> track)
        {
            if (note.value != 95)
                return false;

            difficulties[3].notes[1] = currEvent.position;
            if (!track[3].notes.ValidateLastKey(currEvent.position))
                track[3].notes.Add_NoReturn(currEvent.position);
            return true;
        }

        protected override bool ProcessSpecialNote_Off(ref InstrumentTrack<T> track)
        {
            if (note.value != 95)
                return false;

            long colorPosition = difficulties[3].notes[1];
            if (colorPosition != -1)
            {
                track[3].notes.Traverse_Backwards_Until(colorPosition)[1] = currEvent.position - colorPosition;
                difficulties[3].notes[1] = -1;
            }
            return true;
        }

        protected override void ParseText(ReadOnlySpan<byte> str, ref InstrumentTrack<T> track)
        {
            if (!enableDynamics && str.SequenceEqual(DYNAMICS_STRING))
                enableDynamics = true;
            else
                track.events.Get_Or_Add_Back(currEvent.position).Add(encoding.GetString(str));
        }
    }
}
