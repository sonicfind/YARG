﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Song.Chart.Notes;

namespace YARG.Types
{
    public abstract class PlayablePhrase
    {
        public readonly DualPosition start;
        public readonly DualPosition end;

        protected PlayablePhrase(ref DualPosition start, ref DualPosition end)
        {
            this.start = start;
            this.end = end;
        }
    }

    public abstract class HittablePhrase : PlayablePhrase
    {
        protected readonly int numNotesInPhrase;
        protected int numNotesHit;

        public int NotesInPhrase => numNotesInPhrase;
        public int NotesHit => numNotesHit;

        protected HittablePhrase(int numNotesInPhrase, ref DualPosition start, ref DualPosition end) : base(ref start, ref end)
        {
            this.numNotesInPhrase = numNotesInPhrase;
            numNotesHit = 0;
        }

        public abstract void AddHits(int hits);
    }

    public class OverdrivePhrase : HittablePhrase
    {
        private readonly Player player;
        private bool isValid;

        public OverdrivePhrase(Player player, int numNotesInPhrase, ref DualPosition start, ref DualPosition end) : base(numNotesInPhrase, ref start, ref end)
        {
            this.player = player;
            isValid = true;
        }

        public override void AddHits(int hits)
        {
            if (isValid)
            {
                numNotesHit += hits;
                if (numNotesHit == numNotesInPhrase)
                {
                    Debug.Log($"Overdrive complete: {numNotesHit}");
                    player.CompleteOverdrivePhrase();
                }
            }
        }

        public void Invalidate()
        {
            if (isValid)
            {
                isValid = false;
                player.IncrementOverdrive();
            }
        }
    }

    public class SoloPhrase : HittablePhrase
    {
        private float _percentage;
        public float Percentage => _percentage;

        public SoloPhrase(int numNotesInPhrase, ref DualPosition start, ref DualPosition end) : base(numNotesInPhrase, ref start, ref end)
        {
            _percentage = 0f;
        }

        public override void AddHits(int hits)
        {
            numNotesHit += hits;
            _percentage = numNotesHit / (float) numNotesInPhrase;
        }
    }

    public class OverdriveActivationPhrase : HittablePhrase
    {
        private float _percentage;
        public float Percentage => _percentage;

        public OverdriveActivationPhrase(ref DualPosition start, ref DualPosition end) : base(1, ref start, ref end)
        {
            _percentage = 0f;
        }

        public override void AddHits(int hits)
        {
            numNotesHit += hits;
            _percentage = numNotesHit / (float) numNotesInPhrase;
        }
    }
}
