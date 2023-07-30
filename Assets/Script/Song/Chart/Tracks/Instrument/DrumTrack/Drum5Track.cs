using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart.DrumTrack
{
    public class Midi_Drum5_Loader : Midi_Drum_Loader_Base<Drum_5>
    {
        public Midi_Drum5_Loader(byte multiplierNote) : base(multiplierNote) { }

        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override void ParseLaneColor(ref InstrumentTrack<Drum_5> track)
        {
            int noteValue = note.value - 60;
            int lane = LANEVALUES[noteValue];
            int diffIndex = DIFFVALUES[noteValue];
            if (lane < 7)
            {
                difficulties[diffIndex].notes[lane] = currEvent.position;
                ref var drum = ref track[diffIndex].notes.Get_Or_Add_Back(currEvent.position);
                if (difficulties[diffIndex].Flam)
                    drum.IsFlammed = true;

                if (lane >= 2 && enableDynamics)
                {
                    ref var pad = ref drum.pads[lane - 2];
                    if (note.velocity > 100)
                        pad.Dynamics = DrumDynamics.Accent;
                    else if (note.velocity < 100)
                        pad.Dynamics = DrumDynamics.Ghost;
                }
            }
        }

        protected override void ParseLaneColor_Off(ref InstrumentTrack<Drum_5> track)
        {
            int noteValue = note.value - 60;
            int lane = LANEVALUES[noteValue];
            int diffIndex = DIFFVALUES[noteValue];

            if (lane < 7)
            {
                long colorPosition = difficulties[diffIndex].notes[lane];
                if (colorPosition != -1)
                {
                    track[diffIndex].notes.Traverse_Backwards_Until(colorPosition)[lane] = currEvent.position - colorPosition;
                    difficulties[diffIndex].notes[lane] = -1;
                }
            }
        }

        protected override void ToggleExtraValues(ref InstrumentTrack<Drum_5> track)
        {
            if (note.value == 109)
            {
                for (int i = 0; i < 4; ++i)
                {
                    difficulties[i].Flam = true;
                    if (track[i].notes.ValidateLastKey(currEvent.position))
                        track[i].notes.Last().IsFlammed = true;
                }
            }
        }

        protected override void ToggleExtraValues_Off(ref InstrumentTrack<Drum_5> track)
        {
            if (note.value == 109)
                for (uint i = 0; i < 4; ++i)
                    difficulties[i].Flam = false;
        }
    }
}
