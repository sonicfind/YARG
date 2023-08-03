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
    public struct SubNote
    {
        public readonly int index;
        public readonly long duration;
        public readonly DualPosition endPosition;
        public DualPosition position;
        public HitStatus status;

        public SubNote(int index, ref DualPosition position, long duration, DualPosition endPosition)
        {
            this.index = index;
            this.duration = duration;
            this.endPosition = endPosition;
            this.position = position;
            status = HitStatus.Idle;
        }
    }

#nullable enable
    public abstract class PlayableNote
    {
        public readonly DualPosition position;
        public DualPosition End { get; protected set; } = new(0, 0);
        protected SubNote[] lanes = Array.Empty<SubNote>();
        protected OverdrivePhrase? overdrive = null;
        protected SoloPhrase? solo = null;

        protected PlayableNote(ref DualPosition position)
        {
            this.position = position;
        }

        public string AttachPhrase(PlayablePhrase phrase)
        {
            Debug.Assert(phrase != null);
            if (phrase is OverdrivePhrase overdrivePhrase)
            {
                overdrive = overdrivePhrase;
                return "Overdrive";
            }
            else if (phrase is SoloPhrase soloPhrase)
            {
                solo = soloPhrase;
                return "Solo";
            }
            return string.Empty;
        }

        public abstract HitStatus TryHit(object input, in bool combo);
        public abstract void HandleMiss();
        public abstract HitStatus OnDequeueFromMiss();
        public abstract void Draw(float trackPosition);
    }

    public abstract class Sustained_Playable : PlayableNote
    {
        protected Sustained_Playable(ref DualPosition position) : base(ref position) { }
        public abstract HitStatus UpdateSustain(DualPosition position, object input);
    }
}
