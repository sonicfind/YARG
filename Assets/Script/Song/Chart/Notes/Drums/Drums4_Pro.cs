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

    public unsafe struct Drum_4ProS : IDrumNote_Pro, IReadableFromDotChart
    {
        public uint NumLanes => 6;
        private TruncatableSustain _bass;
        private TruncatableSustain _doubleBass;
        private DrumPad snare;
        private DrumPad yellow;
        private DrumPad blue;
        private DrumPad green;
        private bool cymbal_yellow;
        private bool cymbal_blue;
        private bool cymbal_green;

        public ulong Bass
        {
            get { return _bass; }
            set
            {
                _bass = value;
                _doubleBass.Disable();
            }
        }
        public ulong DoubleBass
        {
            get { return _doubleBass; }
            set
            {
                _doubleBass = value;
                _bass.Disable();
            }
        }

        public ref DrumPad Pads(uint index) { fixed (DrumPad* pads = &snare) return ref pads[index]; }
        public ref bool Cymbals(uint index) { fixed (bool* cymbals = &cymbal_yellow) return ref cymbals[index]; }

        public bool IsFlammed { get; set; }

        public ulong this[uint lane]
        {
            get
            {
                if (lane == 0)
                    return _bass;

                if (lane == 1)
                    return _doubleBass;

                return Pads(lane - 2).Duration;
            }

            set
            {
                if (lane == 0)
                {
                    _bass = value;
                    _doubleBass.Disable();
                }
                else if (lane == 1)
                {
                    _doubleBass = value;
                    _bass.Disable();
                }
                else
                    Pads(lane - 2).Duration = value;
            }
        }

        public void Disable(uint lane)
        {
            if (lane == 0)
                _bass.Disable();
            else if (lane == 1)
                _doubleBass.Disable();
            else
                Pads(lane - 2).Disable();
        }

        public Drum_4ProS(ref Drum_LegacyS drum)
        {
            _bass = drum.Bass > 0 ? drum.Bass : default;
            _doubleBass = drum.DoubleBass > 0 ? drum.DoubleBass : default;

            snare = default;
            yellow = default;
            blue = default;
            green = default;

            fixed (DrumPad* pads = &snare)
                for (uint i = 0; i < 4; ++i)
                    pads[i] = drum.Pads(i);

            cymbal_yellow = drum.Cymbals(0);
            cymbal_blue = drum.Cymbals(1);
            cymbal_green = drum.Cymbals(2);
            IsFlammed = drum.IsFlammed;
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane == 0) _bass = length;
            else if (lane <= 4) Pads(lane - 1).Duration = length;
            else if (lane == 32)
            {
                _doubleBass = _bass;
                _bass.Disable();
            }
            else if (66 <= lane && lane <= 68) Cymbals(lane - 66) = true;
            else if (34 <= lane && lane <= 37) Pads(lane - 34).Dynamics = DrumDynamics.Accent;
            else if (40 <= lane && lane <= 43) Pads(lane - 40).Dynamics = DrumDynamics.Ghost;
            else
                return false;
            return true;
        }

        public IPlayableNote ConvertToPlayable<T>(in ulong position, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S
        {
            throw new NotImplementedException();
        }

        public ulong GetLongestSustain()
        {
            ulong sustain = _bass;
            if (_doubleBass > sustain)
                sustain = _doubleBass;

            fixed (DrumPad* pads = &snare)
            {
                for (int i = 0; i < 4; ++i)
                {
                    ulong end = pads[i].Duration;
                    if (end > sustain)
                        sustain = end;
                }
            }
            return sustain;
        }

        public bool HasActiveNotes()
        {
            fixed (DrumPad* pads = &snare)
                for (int i = 0; i < 4; ++i)
                    if (pads[i].IsActive())
                        return true;
            return false;
        }
    }
}
