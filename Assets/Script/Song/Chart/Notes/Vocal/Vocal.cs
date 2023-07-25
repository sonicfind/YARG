using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public struct Vocal : IPitched
    {
        public string lyric;
        public NormalizedDuration duration;

        private PitchName _note;
        private int _octave;
        private uint _binary;

        public int OCTAVE_MIN => 2;
        public int OCTAVE_MAX => 6;

        public PitchName Note
        {
            get { return _note; }
            set
            {
                uint binaryNote = IPitched.ThrowIfInvalidPitch(this, value);
                _note = value;
                _binary = (uint) (_octave + 1) * IPitched.OCTAVE_LENGTH + binaryNote;
            }
        }
        public int Octave
        {
            get { return _octave; }
            set
            {
                uint binaryOctave = IPitched.ThrowIfInvalidOctave(this, value);
                _octave = value;
                _binary = (uint) _note + binaryOctave;
            }
        }
        public uint Binary
        {
            get { return _binary; }
            set
            {
                var combo = IPitched.SplitBinary(this, value);
                _binary = value;
                _octave = combo.Item1;
                _note = combo.Item2;
            }
        }

        public bool IsPlayable() { return lyric.Length > 0 && (_octave >= 2 || lyric[0] == '#'); }

        public Vocal(string lyric)
        {
            this.lyric = lyric;
            duration = default;
            _note = PitchName.C;
            _octave = 0;
            _binary = 0;
        }
    }
}
