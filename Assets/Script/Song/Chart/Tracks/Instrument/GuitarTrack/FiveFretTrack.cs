using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart.GuitarTrack
{
    public class Midi_FiveFret_Loader : Midi_Instrument_Loader<FiveFret>
    {
        internal static readonly byte[][] ENHANCED_STRINGS = new byte[][] { Encoding.ASCII.GetBytes("[ENHANCED_OPENS]"), Encoding.ASCII.GetBytes("ENHANCED_OPENS") };
        private class FiveFret_MidiDiff
        {
            internal static readonly byte[] PHRASE = new byte[] { 0 };

            public bool SliderNotes { get; set; }
            public bool HopoOn { get; set; }
            public bool HopoOff { get; set; }
            public readonly long[] notes = new long[6] { -1, -1, -1, -1, -1, -1 };
            public readonly Midi_PhraseList phrases;

            public FiveFret_MidiDiff()
            {
                phrases = new(new (byte[], Midi_Phrase)[] {
                    new(PHRASE, new(SpecialPhraseType.StarPower_Diff)),
                    new(PHRASE, new(SpecialPhraseType.FaceOff_Player1)),
                    new(PHRASE, new(SpecialPhraseType.FaceOff_Player2)),
                });
            }

            static FiveFret_MidiDiff() {}
        }

        private readonly FiveFret_MidiDiff[] difficulties = new FiveFret_MidiDiff[4] { new(), new(), new(), new(), };
        private readonly int[] lanes = new int[] {
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
            13, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11,
        };

        static Midi_FiveFret_Loader() { }

        public Midi_FiveFret_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        protected override bool IsNote() { return 59 <= note.value && note.value <= 107; }

        protected override void ParseLaneColor(ref InstrumentTrack<FiveFret> track)
        {
            int noteValue = note.value - 59;
            int lane = lanes[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            
            if (lane < 6)
            {
                ref var diff = ref difficulties[diffIndex];
                diff.notes[lane] = currEvent.position;
                if (!track[diffIndex].notes.ValidateLastKey(currEvent.position))
                {
                    if (track[diffIndex].notes.Capacity == 0)
                        track[diffIndex].notes.Capacity = 5000;

                    ref var guitar = ref track[diffIndex].notes.Add(currEvent.position);
                    if (diff.SliderNotes)
                        guitar.IsTap = true;

                    if (diff.HopoOn)
                        guitar.Forcing = ForceStatus.HOPO;
                    else if (diff.HopoOff)
                        guitar.Forcing = ForceStatus.STRUM;
                }
            }
            else if (lane == 6)
            {
                difficulties[diffIndex].HopoOn = true;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.HOPO;
            }
            // HopoOff marker
            else if (lane == 7)
            {
                difficulties[diffIndex].HopoOff = true;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.STRUM;
            }
            else if (lane == 8)
            {
                if (diffIndex == 3)
                {
                    phrases.AddPhrase(ref track.specialPhrases, currEvent.position, SpecialPhraseType.Solo, 100);
                    return;
                }

                for (int i = 0; i < 4; ++i)
                    lanes[12 * i + 8] = 12;

                for (int i = 0; i < track.specialPhrases.Count;)
                {
                    var vec = track.specialPhrases.At_index(i);
                    var phrases = vec.obj;
                    for (int p = 0; p < phrases.Count;)
                    {
                        if (phrases[p].Type == SpecialPhraseType.Solo)
                        {
                            track[3].specialPhrases[vec.key].Add(new(SpecialPhraseType.StarPower_Diff, phrases[p].Duration));
                            vec.obj.RemoveAt(p);
                        }
                        else
                            ++p;
                    }

                    if (vec.obj.Count == 0)
                        track.specialPhrases.RemoveAt(i);
                    else
                        ++i;
                }

                difficulties[diffIndex].phrases.AddPhrase(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.StarPower_Diff, 100);
            }
            else if (lane == 9)
                difficulties[diffIndex].SliderNotes = true;
            else if (lane == 10)
                difficulties[diffIndex].phrases.AddPhrase(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.FaceOff_Player1, 100);
            else if (lane == 11)
                difficulties[diffIndex].phrases.AddPhrase(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.FaceOff_Player2, 100);
            else if (lane == 12)
                difficulties[diffIndex].phrases.AddPhrase(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.StarPower_Diff, 100);
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<FiveFret> track)
        {
            int noteValue = note.value - 59;
            int lane = lanes[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            if (lane < 6)
            {
                long colorPosition = difficulties[diffIndex].notes[lane];
                if (colorPosition != -1)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[(int)lane] = currEvent.position - colorPosition;
                    difficulties[diffIndex].notes[lane] = -1;
                }
            }
            else if (lane == 6)
            {
                difficulties[diffIndex].HopoOn = false;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.NATURAL;
            }
            else if (lane == 7)
            {
                difficulties[diffIndex].HopoOff = false;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.NATURAL;
            }
            else if (lane == 8)
                phrases.AddPhrase_Off(ref track.specialPhrases, currEvent.position, SpecialPhraseType.Solo);
            else if (lane == 9)
                difficulties[diffIndex].SliderNotes = false;
            else if (lane == 10)
                difficulties[diffIndex].phrases.AddPhrase_Off(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.FaceOff_Player1);
            else if (lane == 11)
                difficulties[diffIndex].phrases.AddPhrase_Off(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.FaceOff_Player2);
            else if (lane == 12)
                difficulties[diffIndex].phrases.AddPhrase_Off(ref track[diffIndex].specialPhrases, currEvent.position, SpecialPhraseType.StarPower_Diff);
        }

        protected override void ParseSysEx(ReadOnlySpan<byte> str, ref InstrumentTrack<FiveFret> track)
        {
            if (str.StartsWith(SYSEXTAG))
            {
                if (str[6] == 1)
                    NormalizeNoteOnPosition();

                if (str[4] == (char)0xFF)
                {
                    switch (str[5])
                    {
                        case 1:
                            {
                                int status = str[6] == 0 ? 1 : 0;
                                for (int diff = 0; diff < 4; ++diff)
                                    lanes[12 * diff + 1] = status;
                                break;
                            }
                        case 4:
                            {
                                for (int diff = 0; diff < 4; ++diff)
                                {
                                    difficulties[diff].SliderNotes = str[6] == 1;
                                    if (track[diff].notes.ValidateLastKey(currEvent.position))
                                        track[diff].notes.Last().IsTap = str[6] == 1;
                                }
                                break;
                            }
                    }
                }
                else
                {
                    byte diff = str[4];
                    switch (str[5])
                    {
                        case 1:
                            lanes[12 * diff + 1] = str[6] == 0 ? 1 : 0;
                            break;
                        case 4:
                            {
                                bool enable = str[6] == 1;
                                difficulties[diff].SliderNotes = enable;
                                if (track[diff].notes.ValidateLastKey(currEvent.position))
                                    track[diff].notes.Last().IsTap = enable;
                            }
                            break;
                    }
                }
            }
        }

        protected override void ParseText(ReadOnlySpan<byte> str, ref InstrumentTrack<FiveFret> track)
        {
            if (lanes[0] == 13 && (str.SequenceEqual(ENHANCED_STRINGS[0]) || str.SequenceEqual(ENHANCED_STRINGS[1])))
            {
                for (int diff = 0; diff < 4; ++diff)
                    lanes[12 * diff] = 0;
            }
            else
                track.events.Get_Or_Add_Back(currEvent.position).Add(encoding.GetString(str));
        }
    }
}
