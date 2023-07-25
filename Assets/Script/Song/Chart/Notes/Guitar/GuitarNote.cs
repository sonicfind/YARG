using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public enum ForceStatus
    {
        NATURAL,
        FORCED_LEGACY,
        HOPO,
        STRUM
    }

    public enum PlayableGuitarType
    {
        STRUM,
        HOPO,
        TAP
    }

    public class PlayableNote_Guitar : PlayableNote
    {
        private readonly string mesh;
        private readonly PlayableGuitarType type;

        public PlayableNote_Guitar(string mesh, PlayableGuitarType type, List<SubNote> lanes) : base(lanes)
        {
            this.mesh = mesh;
            this.type = type;
        }

        public override HitStatus TryHit(object input, in List<(ulong, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }

        public override HitStatus TryHit_InCombo(object input, in List<(ulong, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }
    }

    public abstract class GuitarNote : Note<TruncatableSustain>
    {
        public static ulong HopoFrequency { get; set; }
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        protected GuitarNote(int numColors) : base(numColors) { }

        public ulong this[uint lane]
        {
            get
            {
                return lanes[lane];
            }

            set
            {
                lanes[lane] = value;
                if (lane == 0)
                {
                    for (int i = 1; i < lanes.Length; ++i)
                        lanes[i].Disable();
                }
                else
                    lanes[0].Disable();
            }
        }

        public void Disable(uint lane)
        {
            lanes[lane].Disable();
        }

#nullable enable
        protected (PlayableGuitarType, List<SubNote>) ConstructTypeAndNotes(in ulong position, in ulong prevPosition, in GuitarNote? prevNote)
        {
            var notes = new List<SubNote>();
            for (int i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    notes.Add(new(i, position + lanes[i].Duration));

            PlayableGuitarType type;
            if (IsTap)
            {
                type = PlayableGuitarType.TAP;
            }
            else
            {
                var forcing = Forcing;
                if (forcing == ForceStatus.STRUM)
                {
                    type = PlayableGuitarType.STRUM;
                }
                else if (forcing == ForceStatus.HOPO)
                {
                    type = PlayableGuitarType.HOPO;
                }
                else
                {
                    bool isStrum = IsChorded() || prevNote == null || IsContainedIn(prevNote) || position > prevPosition + HopoFrequency;
                    type = isStrum != (forcing == ForceStatus.FORCED_LEGACY) ? PlayableGuitarType.STRUM : PlayableGuitarType.HOPO;
                }
            }
            return new(type, notes);
        }

        public bool IsChorded()
        {
            if (lanes[0].IsActive())
                return false;

            int num = 0;
            for (int i = 1; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    num++;
            return num > 1;
        }

        public bool HasSameFretting(GuitarNote note)
        {
            if (lanes[0].IsActive())
                return note.lanes[0].IsActive();

            for (uint i = 1; i < lanes.Length; ++i)
                if (lanes[i].IsActive() != note.lanes[i].IsActive())
                    return false;
            return true;
        }

        // Assumes the current note is NOT a chord
        public bool IsContainedIn(GuitarNote note)
        {
            for (uint i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return note.lanes[i].IsActive();
            return false;
        }
    }
}
