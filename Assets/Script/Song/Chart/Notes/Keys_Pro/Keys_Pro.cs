using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public struct Pitched_Key : IPitched, IEnableable
    {
        private TruncatableSustain _duration;
        private PitchName _note;
        private int _octave;
        private int _binary;

        public long Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public int OCTAVE_MIN => 3;
        public int OCTAVE_MAX => 5;

        public PitchName Note
        {
            get { return _note; }
            set
            {
                int binaryNote = IPitched.ThrowIfInvalidPitch(this, value);
                _note = value;
                _binary = (_octave + 1) * IPitched.OCTAVE_LENGTH + binaryNote;
            }
        }
        public int Octave
        {
            get { return _octave; }
            set
            {
                int binaryOctave = IPitched.ThrowIfInvalidOctave(this, value);
                _octave = value;
                _binary = (int) _note + binaryOctave;
            }
        }
        public int Binary
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

        public bool IsActive() { return _duration.IsActive(); }
        public void Disable()
        {
            _duration.Disable();
            _note = PitchName.C;
            _octave = 0;
            _binary = 0;
        }

        public Pitched_Key(long length)
        {
            _duration = length;
            _note = PitchName.C;
            _octave = 0;
            _binary = 0;
        }

        public Pitched_Key(long length, int binary) : this (length)
        {
            Binary = binary;
        }

        public Pitched_Key(long length, PitchName note, int octave) : this(length)
        {
            Note = note;
            Octave = octave;
        }
    }

    public class Keys_Pro : Note<Pitched_Key>
    {
        public override int NumLanes => 48;
        public uint NumActive { get; private set; } = 0;
        public Keys_Pro() : base(4) { }

        public Pitched_Key this[uint index]
        {
            get
            {
                if (index >= NumActive)
                    throw new IndexOutOfRangeException();
                return lanes[index];
            }
            set
            {
                if (index < NumActive)
                {
                    if (!value.IsActive())
                    {
                        --NumActive;
                        for (uint i = index; i < NumActive; ++i)
                            lanes[i] = lanes[i + 1];
                    }
                    else
                        lanes[index] = value;
                }
                else if (value.IsActive())
                {
                    if (index == NumActive && NumActive < 4)
                        AddNote(value, value.Binary);
                    else
                        throw new IndexOutOfRangeException();
                }
            }
        }

        public bool Add(int binary, long length)
        {
            if (NumActive == 4)
                return false;

            Pitched_Key key = new(length, binary);
            AddNote(key, binary);
            return true;
        }

        public bool Add(PitchName note, int octave, long length)
        {
            if (NumActive == 4)
                return false;

            Pitched_Key key = new(length, note, octave);
            AddNote(key, key.Binary);
            return true;
        }

        public void SetLength(uint index, long length)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            lanes[index].Duration = length;
        }

        public void SetPitch(uint index, PitchName note)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            lanes[index].Note = note;
        }

        public void SetOctave(uint index, int octave)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            lanes[index].Octave = octave;
        }
        public void SetBinary(uint index, int binary)
        {
            if (index >= NumActive)
                throw new IndexOutOfRangeException();
            lanes[index].Binary = binary;
        }

        private void AddNote(Pitched_Key key, int binary)
        {
            uint i = 0;
            while (i < NumActive)
            {
                int cmp = lanes[i].Binary;
                if (cmp == binary)
                    throw new Exception("Duplicate pitches are not allowed");

                if (cmp > binary)
                {
                    for (uint j = NumActive; j > i; --j)
                        lanes[j] = lanes[j - 1];
                    break;
                }
                ++i;
            }
            lanes[i] = key;
            NumActive++;
        }

        public override uint GetNumActive()
        {
            return NumActive;
        }

        public override bool HasActiveNotes()
        {
            return NumActive > 0;
        }

        public override long GetLongestSustain()
        {
            long sustain = 0;
            for (int i = 0; i < NumActive; ++i)
            {
                long end = lanes[i].Duration;
                if (end > sustain)
                    sustain = end;
            }
            return sustain;
        }

#nullable enable
        public override PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }
}
