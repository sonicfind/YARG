using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public abstract class Note<NoteType> : INote
        where NoteType : struct, IEnableable
    {
        protected readonly NoteType[] lanes;
        protected Note(int numcolors)
        {
            lanes = new NoteType[numcolors];
        }

        public virtual bool HasActiveNotes()
        {
            for (int i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return true;
            return false;
        }

        public virtual ulong GetLongestSustain()
        {
            ulong sustain = 0;
            for (int i = 0; i < lanes.Length; ++i)
            {
                ulong end = lanes[i].Duration;
                if (end > sustain)
                    sustain = end;
            }
            return sustain;
        }

#nullable enable
        public abstract IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote);
    }
}
