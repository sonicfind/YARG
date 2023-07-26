using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Song.Chart.ProKeysTrack
{
    public enum ProKey_Ranges
    {
        C1_E2,
        D1_F2,
        E1_G2,
        F1_A2,
        G1_B2,
        A1_C3,
    };

    public class ProKeysDifficulty : DifficultyTrack<Keys_Pro_S>
    {
        public readonly TimedNativeFlatMap<ProKey_Ranges> ranges = new();

        public override bool IsOccupied() { return !ranges.IsEmpty() || base.IsOccupied(); }
        public override void Clear()
        {
            base.Clear();
            ranges.Clear();
        }
    }
}
