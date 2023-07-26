﻿using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart.GuitarTrack
{
    public class Midi_SixFret_Loader : Midi_Instrument_Loader<SixFret_S>
    {
        private class SixFret_MidiDiff
        {
            public bool SliderNotes { get; set; }
            public bool HopoOn { get; set; }
            public bool HopoOff { get; set; }
            public readonly ulong[] notes = new ulong[7] { ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue };
            public SixFret_MidiDiff() { }
        }

        private readonly SixFret_MidiDiff[] difficulties = new SixFret_MidiDiff[4] { new(), new(), new(), new(), };
        private static readonly uint[] lanes = new uint[] {
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
            0, 4, 5, 6, 1, 2, 3, 7, 8, 9, 10, 11,
        };

        public Midi_SixFret_Loader(byte multiplierNote) : base(
            new(new (byte[], Midi_Phrase)[] {
                new(SOLO, new(SpecialPhraseType.Solo)),
                new(new byte[]{ multiplierNote }, new(SpecialPhraseType.StarPower)),
                new(TRILL, new(SpecialPhraseType.Tremolo)),
                new(TREMOLO, new(SpecialPhraseType.Trill))
            }))
        { }

        static Midi_SixFret_Loader() { }

        protected override bool IsNote() { return 58 <= note.value && note.value <= 103; }

        protected override void ParseLaneColor(ref InstrumentTrack<SixFret_S> track)
        {
            uint noteValue = note.value - 58;
            uint lane = lanes[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            
            if (lane < 7)
            {
                ref var diff = ref difficulties[diffIndex];
                diff.notes[lane] = currEvent.position;
                if (!track[diffIndex].notes.ValidateLastKey(currEvent.position))
                {
                    ref var guitar = ref track[diffIndex].notes.Add(currEvent.position);
                    if (diff.SliderNotes)
                        guitar.IsTap = true;

                    if (diff.HopoOn)
                        guitar.Forcing = ForceStatus.HOPO;
                    else if (diff.HopoOff)
                        guitar.Forcing = ForceStatus.STRUM;
                }
            }
            else if (lane == 7)
            {
                difficulties[diffIndex].HopoOn = true;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.HOPO;
            }
            // HopoOff marker
            else if (lane == 8)
            {
                difficulties[diffIndex].HopoOff = true;
                if (track[diffIndex].notes.ValidateLastKey(currEvent.position))
                    track[diffIndex].notes.Last().Forcing = ForceStatus.STRUM;
            }
            else if (lane == 10)
                difficulties[diffIndex].SliderNotes = true;
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<SixFret_S> track)
        {
            uint noteValue = note.value - 58;
            uint lane = lanes[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            if (lane < 7)
            {
                ulong colorPosition = difficulties[diffIndex].notes[lane];
                if (colorPosition != ulong.MaxValue)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[lane] = currEvent.position - colorPosition;
                    difficulties[diffIndex].notes[lane] = ulong.MaxValue;
                }
            }
            else if (lane == 7)
                difficulties[diffIndex].HopoOn = false;
            else if (lane == 8)
                difficulties[diffIndex].HopoOff = false;
            else if (lane == 10)
                difficulties[diffIndex].SliderNotes = false;
        }

        protected override void ParseSysEx(ReadOnlySpan<byte> str, ref InstrumentTrack<SixFret_S> track)
        {
            if (str.StartsWith(SYSEXTAG))
            {
                if (str[6] == 1)
                    NormalizeNoteOnPosition();

                if (str[5] == 4)
                {
                    if (str[4] == (char)0xFF)
                    {
                        for (int diff = 0; diff < 4; ++diff)
                        {
                            difficulties[diff].SliderNotes = str[6] == 1;
                            if (str[6] == 1 && track[diff].notes.ValidateLastKey(currEvent.position))
                                track[diff].notes.Last().IsTap = true;
                        }
                    }
                    else
                    {
                        byte diff = str[4];
                        if (str[6] == 1)
                        {
                            difficulties[diff].SliderNotes = true;
                            if (track[diff].notes.ValidateLastKey(currEvent.position))
                                track[diff].notes.Last().IsTap = true;
                        }
                        else
                            difficulties[diff].SliderNotes = false;
                    }
                }
            }
        }
    }
}
