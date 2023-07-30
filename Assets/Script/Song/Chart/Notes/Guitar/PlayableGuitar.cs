using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public enum PlayableGuitarType
    {
        STRUM,
        HOPO,
        TAP
    }

    public abstract class Playable_Guitar : PlayableNote
    {
        public static long HopoFrequency { get; set; }
        protected readonly PlayableGuitarType type;

        protected Playable_Guitar(in long position, in GuitarNote note, SyncTrack sync, in long prevPosition, in GuitarNote? prevNote)
        {
            type = GetGuitarType(position, note, prevPosition, prevNote);
            for (int i = 0; i < note.NumLanes; i++)
            {
                long duration = note[i];
                if (duration > 0)
                    lanes.Add(new(i, new(position + duration, sync)));
            }
        }

        public override HitStatus TryHit(object input, in bool combo, in List<(float, object)> inputSnapshots)
        {
            if (overdrive != null)
            {
                overdrive.AddHits(1);
            }

            if (solo != null)
            {
                solo.AddHits(1);
            }
            return HitStatus.Hit;
        }

        public override void HandleMiss()
        {
            if (overdrive != null)
                overdrive.Invalidate();
        }

        public override (HitStatus, int) UpdateSustain(object input, in List<(float, object)> inputSnapshots)
        {
            return new(HitStatus.Hit, 20);
        }

        public abstract override void Draw(float trackPosition);

        private static PlayableGuitarType GetGuitarType(in long position, in GuitarNote note, in long prevPosition, in GuitarNote? prevNote)
        {
            if (note.IsTap)
                return PlayableGuitarType.TAP;

            var forcing = note.Forcing;
            if (forcing == ForceStatus.STRUM)
                return PlayableGuitarType.STRUM;

            if (forcing == ForceStatus.HOPO)
                return PlayableGuitarType.HOPO;


            bool isStrum = note.IsChorded() || prevNote == null || note.IsContainedIn(prevNote) || position > prevPosition + HopoFrequency;
            return isStrum != (forcing == ForceStatus.FORCED_LEGACY) ? PlayableGuitarType.STRUM : PlayableGuitarType.HOPO;
        }
    }
}
