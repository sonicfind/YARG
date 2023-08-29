using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Chart;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public enum ForceStatus
    {
        NATURAL,
        FORCED_LEGACY,
        HOPO,
        STRUM
    }

    public abstract class GuitarNote : Note<TruncatableSustain>
    {
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        protected GuitarNote(int numColors) : base(numColors) { }

        public long this[int lane]
        {
            get
            {
                return lanes[lane];
            }

            set
            {
                lanes[lane] = value;
                if (lane == 0)
                {
                    for (int i = 1; i < lanes.Length; ++i)
                        lanes[i].Disable();
                }
                else
                    lanes[0].Disable();
            }
        }

        public void Disable(uint lane)
        {
            lanes[lane].Disable();
        }

        public override int GetNumActive()
        {
            for (int i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return 1;
            return 0;
        }

        public bool IsChorded()
        {
            if (lanes[0].IsActive())
                return false;

            int num = 0;
            for (int i = 1; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    num++;
            return num > 1;
        }

        public bool StartsWith(GuitarNote note)
        {
            for (uint i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return note.lanes[i].IsActive();
            return false;
        }
    }

    

    
}
