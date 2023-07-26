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
        public Drum_Legacy() : base(5) { }

        public bool Set_From_Chart(uint lane, ulong length)
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
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }

    public unsafe struct Drum_LegacyS : IDrumNote_Pro, IReadableFromDotChart
    {
        public uint NumLanes => 7;
        private TruncatableSustain _bass;
        private TruncatableSustain _doubleBass;
        private DrumPad snare;
        private DrumPad yellow;
        private DrumPad blue;
        private DrumPad green4_orange5;
        private DrumPad green5;
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
            {
                Pads(lane - 2).Disable();
            }
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane == 0) _bass = length;
            else if (lane <= 5) Pads(lane - 1).Duration = length;
            else if (lane == 32)
            {
                _doubleBass = _bass;
                _bass.Disable();
            }
            else if (66 <= lane && lane <= 68) Cymbals(lane - 66) = true;
            else if (34 <= lane && lane <= 38) Pads(lane - 34).Dynamics = DrumDynamics.Accent;
            else if (40 <= lane && lane <= 44) Pads(lane - 40).Dynamics = DrumDynamics.Ghost;
            else
                return false;
            return true;
        }

        public DrumType ParseDrumType()
        {
            if (green5.IsActive())
                return DrumType.FIVE_LANE;
            return cymbal_yellow || cymbal_blue || cymbal_green ? DrumType.FOUR_PRO : DrumType.UNKNOWN;
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
                for (int i = 0; i < 5; ++i)
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
                for (int i = 0; i < 5; ++i)
                    if (pads[i].IsActive())
                        return true;
            return false;
        }
    }
}
