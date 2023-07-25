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
}
