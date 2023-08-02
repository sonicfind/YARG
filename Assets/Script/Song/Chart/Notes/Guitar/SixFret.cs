using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.SocialPlatforms;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public class SixFret : GuitarNote, IReadableFromDotChart
    {
        public override int NumLanes => 7;
        public SixFret() : base(7) { }

#nullable enable
        public override PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote)
        {
            return new Playable_SixFret(ref position, this, sync, syncIndex, prevPosition, prevNote as SixFret);
        }

        internal static readonly uint[] SIXFRETLANES = new uint[5] { 4, 5, 6, 1, 2 };
        public bool Set_From_Chart(uint lane, long length)
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

    public class Playable_SixFret : Playable_Guitar
    {
        public Playable_SixFret(ref DualPosition position, in SixFret note, in SyncTrack sync, int syncIndex, in long prevPosition, in SixFret? prevNote)
            : base(ref position, note, sync, syncIndex, prevPosition, prevNote) { }

        public override void Draw(float trackPosition)
        {

        }
    }
}
