using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public class Keys : Note<TruncatableSustain>, IReadableFromDotChart
    {
        public override int NumLanes => 5;
        public Keys() : base(5) { }
        public long this[int lane]
        {
            get { return lanes[lane]; }
            set { lanes[lane] = value; }
        }

        public bool Set_From_Chart(uint lane, long length)
        {
            if (lane >= 5)
                return false;

            lanes[lane] = length;
            return true;
        }

#nullable enable
        public override PlayableNote ConvertToPlayable(in long position, in SyncTrack sync, in long prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }
}
