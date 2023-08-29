using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Player;
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

    public abstract class Playable_Guitar : Sustained_Playable
    {
        public static long HopoFrequency { get; set; }
        protected readonly PlayableGuitarType type;
        protected readonly bool disjointed;

        protected Playable_Guitar(ref DualPosition position, in GuitarNote note, SyncTrack sync, int syncIndex, in long prevPosition, in GuitarNote? prevNote) : base(ref position)
        {
            type = GetGuitarType(note, prevPosition, prevNote);

            List<SubNote> subBuf = new();
            long farthestEnd = position.ticks;
            long prevDuration = 0;
            for (int i = 0; i < note.NumLanes; i++)
            {
                long duration = note[i];
                if (duration > 0)
                {
                    long end = position.ticks + duration;
                    subBuf.Add(new(i, ref position, duration, new(end, sync.ConvertToSeconds(end, syncIndex))));
                    if (end > farthestEnd)
                        farthestEnd = end;

                    if (prevDuration != 0 && duration != end)
                        disjointed = true;
                    prevDuration = duration;
                }
            }
            lanes = subBuf.ToArray();
            End = new(farthestEnd, sync.ConvertToSeconds(farthestEnd, syncIndex));
        }

        public override HitStatus TryHit(object input, in bool combo)
        {
            if (overdrive != null)
            {
                overdrive.AddHits(1);
            }

            if (solo != null)
            {
                solo.AddHits(1);
            }

            var status = HitStatus.Hit;
            for (int i = 0; i < lanes.Length; i++)
            {
                ref var lane = ref lanes[i];
                if (lane.duration > 1)
                    status = lane.status = HitStatus.Sustained;
                else
                    lane.status = HitStatus.Hit;
            }
            return status;
        }

        public override void HandleMiss()
        {
            if (overdrive != null)
                overdrive.Invalidate();
        }

        public override HitStatus OnDequeueFromMiss()
        {
            HandleMiss();

            var status = HitStatus.Missed;
            for (int i = 0; i < lanes.Length; i++)
            {
                ref var lane = ref lanes[i];
                if (lane.duration > 1)
                    status = lane.status = HitStatus.Dropped;
            }
            return status;
        }

        public override HitStatus UpdateSustain(DualPosition position, object input)
        {
            var status = HitStatus.Dropped;
            for (int i = 0; i < lanes.Length; i++)
            {
                ref var lane = ref lanes[i];
                if (lane.status == HitStatus.Sustained)
                {
                    long sustain = position.ticks - lane.position.ticks;
                    if (sustain >= lane.duration)
                    {
                        lane.status = HitStatus.Hit;
                        if (status != HitStatus.Sustained)
                            status = HitStatus.Hit;
                    }
                    else if (true)
                        status = HitStatus.Sustained;
                    else
                    {
                        lane.status = HitStatus.Dropped;
                        lane.position = position;
                    }
                }
            }
            return status;
        }

        public abstract override void Draw(float trackPosition);

        private PlayableGuitarType GetGuitarType(in GuitarNote note, in long prevPosition, in GuitarNote? prevNote)
        {
            if (note.IsTap)
                return PlayableGuitarType.TAP;

            var forcing = note.Forcing;
            if (forcing == ForceStatus.STRUM)
                return PlayableGuitarType.STRUM;

            if (forcing == ForceStatus.HOPO)
                return PlayableGuitarType.HOPO;


            bool isStrum = note.IsChorded() || prevNote == null || prevNote.StartsWith(note) || position.ticks > prevPosition + HopoFrequency;
            return isStrum != (forcing == ForceStatus.FORCED_LEGACY) ? PlayableGuitarType.STRUM : PlayableGuitarType.HOPO;
        }
    }
}
