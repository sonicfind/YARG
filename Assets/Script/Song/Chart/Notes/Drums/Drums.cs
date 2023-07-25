using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public enum DrumDynamics
    {
        None,
        Accent,
        Ghost
    }

    public struct DrumPad : IEnableable
    {
        private TruncatableSustain _duration;

        public ulong Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public DrumDynamics Dynamics { get; set; }

        public bool IsActive()
        {
            return _duration.IsActive();
        }

        public void Disable()
        {
            _duration.Disable();
            Dynamics = DrumDynamics.None;
        }
    }

    public abstract class DrumNote : Note<DrumPad>
    {
        protected TruncatableSustain _bass;
        protected TruncatableSustain _doubleBass;
        public readonly DrumPad[] pads;

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

        public bool IsFlammed { get; set; }

        public ulong this[uint lane]
        {
            get
            {
                if (lane == 0)
                    return _bass;

                if (lane == 1)
                    return _doubleBass;

                return pads[lane - 2].Duration;
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
                    pads[lane - 2].Duration = value;
            }
        }

        protected DrumNote(int numPads) : base(numPads)
        {
            pads = lanes;
        }

        protected DrumNote(int numPads, DrumNote other) : this(numPads)
        {
            _bass = other._bass;
            _doubleBass = other._doubleBass;
            for (int i = 0; i < numPads; ++i)
                pads[i] = other.pads[i];
            IsFlammed = other.IsFlammed;
        }

        public override bool HasActiveNotes()
        {
            return HasActiveNotes() || _bass.IsActive() || _doubleBass.IsActive();
        }
    }

    public abstract class DrumNote_Pro : DrumNote
    {
        public readonly bool[] cymbals = new bool[3];

        protected DrumNote_Pro(int numPads) : base(numPads) { }

        protected DrumNote_Pro(int numPads, DrumNote_Pro other) : base(numPads, other)
        {
            cymbals = other.cymbals;
        }
    }
}
