using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Song.Chart.Vocals
{
    public class VocalTrack : Track
    {
        public readonly TimedNativeFlatMap<VocalPercussion> percussion = new();
        private readonly TimedFlatMap<Vocal>[] vocals;

        public TimedFlatMap<Vocal> this[int trackIndex]
        {
            get { return vocals[trackIndex]; }
        }

        public VocalTrack(int numTracks)
        {
            vocals = new TimedFlatMap<Vocal>[numTracks];
            for (int i = 0; i < numTracks; i++)
                vocals[i] = new();
        }

        public override bool IsOccupied()
        {
            for (int i = 0; i < vocals.Length; i++)
                if (!vocals[i].IsEmpty())
                    return true;
            return !percussion.IsEmpty() || base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            for (int i = 0; i < vocals.Length; i++)
                vocals[i].Clear();
            percussion.Clear();
        }
        public override void TrimExcess()
        {
            for (int i = 0; i < vocals.Length; i++)
            {
                ref var track = ref vocals[i];
                if ((track.Count < 100 || 2000 <= track.Count) && track.Count < track.Capacity)
                    track.TrimExcess();
            }

            if ((percussion.Count < 20 || 400 <= percussion.Count) && percussion.Count < percussion.Capacity)
                percussion.TrimExcess();
        }

        public override long GetLastNoteTime()
        {
            long endTime = 0;
            foreach (var track in vocals)
            {
                if (track.IsEmpty())
                    continue;

                ref var vocal = ref track.At_index(track.Count - 1);
                long end = vocal.key + vocal.obj.duration;
                if (end > endTime)
                    endTime = end;
            }

            if (!percussion.IsEmpty())
            {
                ref var perc = ref percussion.At_index(percussion.Count - 1);
                if (perc.key > endTime)
                    endTime = perc.key;
            }
            return endTime;
        }
    }
}
