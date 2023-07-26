using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Types
{
    public abstract class PlaytimePhrase
    {
        protected readonly int numNotesInPhrase;

        protected PlaytimePhrase(int numNotesInPhrase)
        {
            this.numNotesInPhrase = numNotesInPhrase;
        }
    }

    public class OverdrivePhrase : PlaytimePhrase
    {
        private readonly Player player;
        private int numNotesHit;
        public bool ValidStatus { get; private set; }

        public OverdrivePhrase(Player player, int numNotesInPhrase) : base(numNotesInPhrase)
        {
            this.player = player;
            numNotesHit = 0;
            ValidStatus = true;
        }

        public void AddHits(int hits)
        {
            numNotesHit += hits;
            if (numNotesHit == numNotesInPhrase)
                player.AddOverdrive(.25f);
        }

        public void Invalidate()
        {
            ValidStatus = false;
            player.IncrementOverdrive();
        }
    }

    public class SoloPhrase<T> : PlaytimePhrase
        where T : INote_Base
    {
        private readonly Player_Instrument_Base<T> player;
        private int numNotesHit;

        public SoloPhrase(Player_Instrument_Base<T> player, int numNotesInPhrase) : base(numNotesInPhrase)
        {
            this.player = player;
            numNotesHit = 0;
        }

        public void AddHits(int hits)
        {
            numNotesHit += hits;
            player.UpdateSolo(numNotesHit / (float) numNotesInPhrase);
        }
    }
}
