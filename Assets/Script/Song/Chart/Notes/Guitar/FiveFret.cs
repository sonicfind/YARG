using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    
    public class FiveFret : GuitarNote, IReadableFromDotChart
    {
        public FiveFret() : base(6) { }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            (var type, var notes) = ConstructTypeAndNotes(position, prevPosition, prevNote as FiveFret);
            string mesh = type switch
            {
                PlayableGuitarType.STRUM => "FiveFret",
                PlayableGuitarType.HOPO => "FiveFretHopo",
                PlayableGuitarType.TAP => "FiveFretTap",
                _ => throw new Exception("stoopid")
            };
            return new PlayableNote_Guitar(mesh, type, notes);
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                lanes[lane + 1] = length;
                lanes[0].Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                lanes[0] = length;
                for (uint i = 1; i < 6; ++i)
                    lanes[i].Disable();
            }
            else
                return false;
            return true;
        }
    }

    public unsafe struct FiveFret_S : IGuitarNote, IReadableFromDotChart
    {
        public uint NumLanes => 6;
        private TruncatableSustain open;
        private TruncatableSustain green;
        private TruncatableSustain red;
        private TruncatableSustain yellow;
        private TruncatableSustain blue;
        private TruncatableSustain orange;
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
                        for (int i = 1; i < 6; ++i)
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

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                this[lane + 1] = length;
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
                    for (uint i = 1; i < 6; ++i)
                        lanes[i].Disable();
            }
            else
                return false;
            return true;
        }

        public IPlayableNote ConvertToPlayable<T>(in ulong position, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S
        {
            (var type, var notes) = IGuitarNote.ConstructTypeAndNotes(position, prevPosition, this, (FiveFret_S*)prevNote);
            string mesh = type switch
            {
                PlayableGuitarType.STRUM => "FiveFret",
                PlayableGuitarType.HOPO => "FiveFretHopo",
                PlayableGuitarType.TAP => "FiveFretTap",
                _ => throw new Exception("stoopid")
            };
            return new PlayableNote_Guitar(mesh, type, notes);
        }

        public ulong GetLongestSustain()
        {
            ulong sustain = 0;
            fixed (TruncatableSustain* lanes = &open)
            {
                for (int i = 0; i < 6; ++i)
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
                for (int i = 0; i < 6; ++i)
                    if (lanes[i].IsActive())
                        return true;
            return false;
        }
    }
}
