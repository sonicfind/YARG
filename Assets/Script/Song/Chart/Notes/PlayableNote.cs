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
    public struct SubNote
    {
        public readonly int index;
        public readonly DualPosition endPosition;
        public HitStatus status;

        public SubNote(int index, DualPosition endPosition)
        {
            this.index = index;
            this.endPosition = endPosition;
            status = HitStatus.Idle;
        }
    }

#nullable enable
    public abstract class PlayableNote
    {
        protected readonly List<SubNote> lanes = new();
        protected OverdrivePhrase? overdrive = null;
        protected SoloPhrase? solo = null;
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

        public abstract HitStatus TryHit(object input, in bool combo, in List<(float, object)> inputSnapshots);
        public abstract void HandleMiss();
        public abstract (HitStatus, int) UpdateSustain(object input, in List<(float, object)> inputSnapshots);
        public abstract void Draw(float trackPosition);
    }
}
