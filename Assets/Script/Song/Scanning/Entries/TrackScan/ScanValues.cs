using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan
{
    public unsafe struct ScanValues
    {
        internal static byte[] shifts = { 1, 2, 4, 8, 16 };
        public byte subTracks;
        public sbyte intensity;
        public ScanValues(int _ = 0)
        {
            subTracks = 0;
            intensity = -1;
        }

        public void Set(int subTrack)
        {
            subTracks |= shifts[subTrack];
        }
        public bool this[int subTrack]
        {
            get { return (shifts[subTrack] & subTracks) > 0; }
        }

        public static ScanValues operator |(ScanValues lhs, ScanValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
