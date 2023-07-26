using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Chart;
using YARG.Types;

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

    public struct PlayableNote_Guitar<T> : IPlayableNote
        where T : GuitarNote
    {
        private readonly string mesh;
        private readonly PlayableGuitarType type;
        private readonly TruncatableSustain[] lanes;

        private OverdrivePhrase? overdrive;
        private SoloPhrase<T>? solo;

        public PlayableNote_Guitar(string mesh, PlayableGuitarType type, TruncatableSustain[] lanes)
        {
            this.mesh = mesh;
            this.type = type;
            this.lanes = lanes;
            overdrive = null;
            solo = null;
        }

        public void AttachPhrase(PlaytimePhrase phrase)
        {
            Debug.Assert(phrase != null);
            if (phrase is OverdrivePhrase overdrivePhrase)
                overdrive = overdrivePhrase;
            else if (phrase is SoloPhrase<T> soloPhrase)
                solo = soloPhrase;
        }

        public HitStatus TryHit(object input, in List<(float, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }

        public HitStatus TryHit_InCombo(object input, in List<(float, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }

        public (HitStatus, int) UpdateSustain(object input, in List<(float, object)> inputSnapshots)
        {
            return new(HitStatus.Hit, 20);
        }
    }

    public unsafe struct PlayableNote_Guitar_S<T> : IPlayableNote
        where T : unmanaged, IGuitarNote
    {
        private readonly string mesh;
        private readonly PlayableGuitarType type;
        private readonly TruncatableSustain* lanes;
        private readonly uint numLanes;

        private OverdrivePhrase? overdrive;
        private SoloPhrase<T>? solo;

        public PlayableNote_Guitar_S(string mesh, PlayableGuitarType type, TruncatableSustain* lanes, uint numLanes)
        {
            this.mesh = mesh;
            this.type = type;
            this.lanes = lanes;
            this.numLanes = numLanes;
            overdrive = null;
            solo = null;
        }

        public void AttachPhrase(PlaytimePhrase phrase)
        {
            Debug.Assert(phrase != null);
            if (phrase is OverdrivePhrase overdrivePhrase)
                overdrive = overdrivePhrase;
            else if (phrase is SoloPhrase<T> soloPhrase)
                solo = soloPhrase;
        }

        public HitStatus TryHit(object input, in List<(float, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }

        public HitStatus TryHit_InCombo(object input, in List<(float, object)> inputSnapshots)
        {
            return HitStatus.Hit;
        }

        public (HitStatus, int) UpdateSustain(object input, in List<(float, object)> inputSnapshots)
        {
            return new(HitStatus.Hit, 20);
        }
    }

    public abstract class GuitarNote : Note<TruncatableSustain>
    {
        public static ulong HopoFrequency { get; set; }
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        protected GuitarNote(int numColors) : base(numColors) { }

        public ulong this[int lane]
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
        protected PlayableGuitarType GetGuitarType(in ulong position, in ulong prevPosition, in GuitarNote? prevNote)
        {
            if (IsTap)
                return PlayableGuitarType.TAP;

            var forcing = Forcing;
            if (forcing == ForceStatus.STRUM)
                return PlayableGuitarType.STRUM;

            if (forcing == ForceStatus.HOPO)
                return PlayableGuitarType.HOPO;

                
            bool isStrum = IsChorded() || prevNote == null || IsContainedIn(prevNote) || position > prevPosition + HopoFrequency;
            return isStrum != (forcing == ForceStatus.FORCED_LEGACY) ? PlayableGuitarType.STRUM : PlayableGuitarType.HOPO;
        }

        private bool IsChorded()
        {
            if (lanes[0].IsActive())
                return false;

            int num = 0;
            for (int i = 1; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    num++;
            return num > 1;
        }

        // Assumes the current note is NOT a chord
        private bool IsContainedIn(in GuitarNote note)
        {
            for (uint i = 0; i < lanes.Length; ++i)
                if (lanes[i].IsActive())
                    return note.lanes[i].IsActive();
            return false;
        }
    }

    public unsafe interface IGuitarNote : INote_S
    {
        public static ulong HopoFrequency { get; set; }
        public ForceStatus Forcing { get; set; }
        public bool IsTap { get; set; }
        public void ToggleTap() { IsTap = !IsTap; }

        public ulong this[uint lane] { get; set; }

        public void Disable(uint lane);

#nullable enable
        protected static PlayableGuitarType GetGuitarType<T>(in ulong position, in ulong prevPosition, ref T currNote, in T* prevNote)
            where T : unmanaged, IGuitarNote
        {
            if (currNote.IsTap)
                return PlayableGuitarType.TAP;

            var forcing = currNote.Forcing;
            if (forcing == ForceStatus.STRUM)
                return PlayableGuitarType.STRUM;

            if (forcing == ForceStatus.HOPO)
                return PlayableGuitarType.HOPO;

            bool isStrum = IsChorded(ref currNote) || prevNote == null || ContainedIn(ref currNote, ref *prevNote) || position > prevPosition + HopoFrequency;
            return isStrum != (forcing == ForceStatus.FORCED_LEGACY) ? PlayableGuitarType.STRUM : PlayableGuitarType.HOPO;
        }

        public static bool IsChorded<T>(ref T note)
            where T : unmanaged, IGuitarNote
        {
            if (note[0] > 0)
                return false;

            int num = 0;
            for (uint i = 1; i < note.NumLanes; ++i)
                if (note[i] > 0)
                    num++;
            return num > 1;
        }

        // Assumes the current note is NOT a chord
        public static bool ContainedIn<T>(ref T note, ref T container)
            where T : unmanaged, IGuitarNote
        {
            for (uint i = 0; i < container.NumLanes; ++i)
                if (note[i] > 0)
                    return container[i] > 0;
            return false;
        }
    }
}
