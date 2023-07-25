using YARG.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart
{
    public abstract class Track
    {
        public TimedFlatMap<List<SpecialPhrase>> specialPhrases = new();
        public TimedFlatMap<List<string>> events = new();
        public virtual bool IsOccupied() { return !specialPhrases.IsEmpty() || !events.IsEmpty(); }

        public virtual void Clear()
        {
            specialPhrases.Clear();
            events.Clear();
        }
        public abstract void TrimExcess();
        public abstract ulong GetLastNoteTime();
    }
}
