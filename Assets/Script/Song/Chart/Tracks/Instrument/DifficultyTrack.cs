
using YARG.Song.Chart.Notes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

namespace YARG.Song.Chart
{
    public class DifficultyTrack<T> : Track
        where T : INote, new()
    {
        public readonly TimedFlatMap<T> notes = new();

        public override bool IsOccupied() { return !notes.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            notes.Clear();
        }
        public override void TrimExcess() => notes.TrimExcess();
        public override ulong GetLastNoteTime()
        {
            if (notes.IsEmpty()) return 0;

            var note = notes.At_index(notes.Count - 1);
            return note.key + note.obj.GetLongestSustain();
        }
    }
}
