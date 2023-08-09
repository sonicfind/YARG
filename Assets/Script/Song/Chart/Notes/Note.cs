using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public abstract class Note<NoteType> : INote
        where NoteType : struct, IEnableable
    {
        public abstract int NumLanes { get; }
        protected readonly NoteType[] lanes;
        protected Note(int numcolors)
        {
            lanes = new NoteType[numcolors];
        }

        public virtual int GetNumActive()
        {
            int num = 0;
            for (int i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    ++num;
            return num;
        }

        public virtual bool HasActiveNotes()
        {
            for (int i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return true;
            return false;
        }

        public virtual long GetLongestSustain()
        {
            long sustain = 0;
            for (int i = 0; i < lanes.Length; ++i)
            {
                long end = lanes[i].Duration;
                if (end > sustain)
                    sustain = end;
            }
            return sustain;
        }

#nullable enable
        public abstract PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote);
    }
}
