using YARG.Song.Chart.Notes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using YARG.Types;

namespace YARG.Song.Chart.ProGuitarTrack
{
    public class ProGuitarDifficulty<FretType> : DifficultyTrack<Guitar_Pro<FretType>>
        where FretType : unmanaged, IFretted
    {
        public readonly TimedFlatMap<Arpeggio<FretType>> arpeggios = new();

        public override bool IsOccupied() { return !arpeggios.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            arpeggios.Clear();
        }
    }
}
