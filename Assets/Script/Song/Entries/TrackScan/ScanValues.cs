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
        public byte subTracks;
        public sbyte intensity;
        public ScanValues(sbyte baseIntensity)
        {
            subTracks = 0;
            intensity = baseIntensity;
        }

        public void Set(int subTrack)
        {
            subTracks |= (byte)(1 << subTrack);
        }

        public bool this[int subTrack]
        {
            get { return ((byte)(1 << subTrack) & subTracks) > 0; }
        }

        public static ScanValues operator |(ScanValues lhs, ScanValues rhs)
        {
            lhs.subTracks |= rhs.subTracks;
            return lhs;
        }
    }
}
