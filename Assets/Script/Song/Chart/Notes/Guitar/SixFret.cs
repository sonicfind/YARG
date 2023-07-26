using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public class SixFret : GuitarNote, IReadableFromDotChart
    {
        public SixFret() : base(7) { }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            (var type, var notes) = ConstructTypeAndNotes(position, prevPosition, prevNote as SixFret);
            string mesh = type switch
            {
                PlayableGuitarType.STRUM => "SixFret",
                PlayableGuitarType.HOPO => "SixFretHopo",
                PlayableGuitarType.TAP => "SixFretTap",
                _ => throw new Exception("stoopid")
            };
            return new PlayableNote_Guitar(mesh, type, notes);
        }

        internal static readonly uint[] SIXFRETLANES = new uint[5] { 4, 5, 6, 1, 2 };
        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                lanes[SIXFRETLANES[lane]] = length;
                lanes[0].Disable();
            }
            else if (lane == 8)
            {
                lanes[3] = length;
                lanes[0].Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                lanes[0] = length;
                for (uint i = 1; i < 7; ++i)
                    lanes[i].Disable();
            }
            else
                return false;
            return true;
        }
    }

    public unsafe struct SixFret_S : IGuitarNote, IReadableFromDotChart
    {
        public uint NumLanes => 7;
        private TruncatableSustain open;
        private TruncatableSustain black_1;
        private TruncatableSustain black_2;
        private TruncatableSustain black_3;
        private TruncatableSustain white_1;
        private TruncatableSustain white_2;
        private TruncatableSustain white_3;

        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }

        public ulong this[uint lane]
        {
            get
            {
                fixed (TruncatableSustain* lanes = &open)
                    return lanes[lane];
            }

            set
            {
                fixed (TruncatableSustain* lanes = &open)
                {
                    lanes[lane] = value;
                    if (lane == 0)
                    {
                        for (int i = 1; i < 7; ++i)
                            lanes[i].Disable();
                    }
                    else
                        lanes[0].Disable();
                }
            }
        }

        public void Disable(uint lane)
        {
            fixed (TruncatableSustain* lanes = &open)
                lanes[lane].Disable();
        }

        internal static readonly uint[] SIXFRETLANES = new uint[5] { 4, 5, 6, 1, 2 };
        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                fixed (TruncatableSustain* lanes = &open)
                    lanes[SIXFRETLANES[lane]] = length;
                open.Disable();
            }
            else if (lane == 8)
            {
                black_3 = length;
                open.Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                open = length;
                fixed (TruncatableSustain* lanes = &open)
                    for (uint i = 1; i < 7; ++i)
                        lanes[i].Disable();
            }
            else
                return false;
            return true;
        }

        public IPlayableNote ConvertToPlayable<T>(in ulong position, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S
        {
            (var type, var notes) = IGuitarNote.ConstructTypeAndNotes(position, prevPosition, this, (SixFret_S*) prevNote);
            string mesh = type switch
            {
                PlayableGuitarType.STRUM => "SixFret",
                PlayableGuitarType.HOPO => "SixFretHopo",
                PlayableGuitarType.TAP => "SixFretTap",
                _ => throw new Exception("stoopid")
            };
            return new PlayableNote_Guitar(mesh, type, notes);
        }

        public ulong GetLongestSustain()
        {
            ulong sustain = 0;
            fixed (TruncatableSustain* lanes = &open)
            {
                for (int i = 0; i < 7; ++i)
                {
                    ulong end = lanes[i].Duration;
                    if (end > sustain)
                        sustain = end;
                }
            }
            return sustain;
        }

        public bool HasActiveNotes()
        {
            fixed (TruncatableSustain* lanes = &open)
                for (int i = 0; i < 7; ++i)
                    if (lanes[i].IsActive())
                        return true;
            return false;
        }
    }
}
