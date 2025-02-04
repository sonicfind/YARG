using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core;

namespace YARG.Assets.Script.Scores
{
    public enum PlayRecordSortOrder
    {
        Score,
        NotesHit
    }

    public struct PlayRecordEntry : IComparable<PlayRecordEntry>
    {
        public static PlayRecordSortOrder SortOrder { get; set; } = PlayRecordSortOrder.Score;

        public Instrument Instrument;
        public Difficulty Difficulty;
        public long Score;
        public int NotesHit;
        public int MaxStreak;
        public DateTime TimeCreated;
        public int EnginePresetId;

        public int CompareTo(PlayRecordEntry other)
        {
            if (Instrument != other.Instrument)
            {
                return Instrument.CompareTo(other.Instrument);
            }
            if (Difficulty != other.Difficulty)
            {
                // We want reverse ordering (X+ down), so we negate the result
                return -Difficulty.CompareTo(other.Difficulty);
            }
            if (SortOrder == PlayRecordSortOrder.Score)
            {
                if (Score != other.Score)
                {
                    return Score.CompareTo(other.Score);
                }
                if (NotesHit != other.NotesHit)
                {
                    return NotesHit.CompareTo(other.NotesHit);
                }
            }
            else
            {
                if (NotesHit != other.NotesHit)
                {
                    return NotesHit.CompareTo(other.NotesHit);
                }
                if (Score != other.Score)
                {
                    return Score.CompareTo(other.Score);
                }
            }
            if (MaxStreak != other.MaxStreak)
            {
                return MaxStreak.CompareTo(other.MaxStreak);
            }
            return TimeCreated.CompareTo(other.TimeCreated);
        }
    }

    internal class PlayerRecord
    {
        private readonly List<PlayRecordEntry> _entries;
        public Guid PlayerId { get; }

        public PlayerRecord(Guid playerId)
        {
            PlayerId = playerId;
            _entries = new();
        }

        public void Add(PlayRecordEntry entry)
        {
            int index = _entries.BinarySearch(entry);
            if (index < 0)
            {
                _entries.Insert(~index, entry);
            }
        }

        public bool GetBest(Instrument instrument, out PlayRecordEntry bestEntry)
        {
            int index = 0;
            while (index < _entries.Count)
            {
                bestEntry = _entries[index];
                if (bestEntry.Instrument == instrument)
                {
                    return true;
                }
                if (bestEntry.Instrument > instrument)
                {
                    return false;
                }
                ++index;
            }
            bestEntry = default;
            return false;
        }

        public bool GetBest(Instrument instrument, Difficulty difficulty, out PlayRecordEntry bestEntry)
        {
            int index = 0;
            while (index < _entries.Count)
            {
                bestEntry = _entries[index];
                if (bestEntry.Instrument == instrument && bestEntry.Difficulty == difficulty)
                {
                    return true;
                }
                if (bestEntry.Instrument > instrument || bestEntry.Difficulty > difficulty)
                {
                    return false;
                }
            }
            bestEntry = default;
            return false;
        }

        public void ReSort()
        {
            for (int pos = 0; pos < _entries.Count;)
            {
                // We don't need to re-sort the entire list in one go.
                // A change to the sort order only affects the positioning within
                // a instrument-difficulty range. The ranges themselves won't move.
                var entry = _entries[pos];
                int count = 1;
                while (pos + count < _entries.Count)
                {
                    var curr = _entries[pos + count];
                    if (curr.Instrument != entry.Instrument || curr.Difficulty != entry.Difficulty)
                    {
                        break;
                    }
                    count++;
                }

                if (count > 1)
                {
                    _entries.Sort(pos, count, null);
                }
                pos += count;
            }
        }
    }
}
