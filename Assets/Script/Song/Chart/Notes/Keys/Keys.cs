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
        public Keys() : base(5) { }
        public ulong this[uint lane]
        {
            get { return lanes[lane]; }
            set { lanes[lane] = value; }
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane >= 5)
                return false;

            lanes[lane] = length;
            return true;
        }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }

    public unsafe struct Keys_S : INote_S, IReadableFromDotChart
    {
        public uint NumLanes => 5;
        private TruncatableSustain lanes_0;
        private TruncatableSustain lanes_1;
        private TruncatableSustain lanes_2;
        private TruncatableSustain lanes_3;
        private TruncatableSustain lanes_4;
        public ulong this[uint lane]
        {
            get { fixed (TruncatableSustain* lanes = &lanes_0) return lanes[lane]; }
            set { fixed (TruncatableSustain* lanes = &lanes_0) lanes[lane] = value; }
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane >= 5)
                return false;

            fixed (TruncatableSustain* lanes = &lanes_0)
                lanes[lane] = length;
            return true;
        }

        public IPlayableNote ConvertToPlayable<T>(in ulong position, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S
        {
            throw new NotImplementedException();
        }

        public ulong GetLongestSustain()
        {
            ulong sustain = 0;
            fixed (TruncatableSustain* lanes = &lanes_0)
            {
                for (int i = 0; i < 5; ++i)
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
            fixed (TruncatableSustain* lanes = &lanes_0)
                for (int i = 0; i < 5; ++i)
                    if (lanes[i].IsActive())
                        return true;
            return false;
        }
    }
}
