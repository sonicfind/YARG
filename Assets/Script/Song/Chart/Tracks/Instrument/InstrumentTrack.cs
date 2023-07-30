using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart
{
    public class InstrumentTrack_Base<Difficulty> : Track
        where Difficulty : Track, IDifficultyTrack, new()
    {
        protected readonly Difficulty[] difficulties = new Difficulty[4] { new(), new(), new(), new(), };
        public override bool IsOccupied()
        {
            for (int i = 0; i < 4; ++i)
                if (difficulties[i].IsOccupied())
                    return true;
               
            return base.IsOccupied();
        }
        public override void Clear()
        {
            base.Clear();
            for (int i = 0; i < 4; ++i)
                difficulties[i].Clear();
        }
        public override void TrimExcess()
        {
            for (int i = 0; i < 4; ++i)
                difficulties[i].TrimExcess();
        }
        public ref Difficulty this[int index] { get { return ref difficulties[index]; } }

        public override long GetLastNoteTime()
        {
            long endTime = 0;
            for (int i = 0; i < difficulties.Length; ++i)
            {
                long end = difficulties[i].GetLastNoteTime();
                if (end > endTime)
                    endTime = end;
            }
            return endTime;
        }

        public Player[] SetupPlayers(KeyValuePair<int, List<(GameObject track, PlayerManager.Player)>>[] playerMapping, SyncTrack sync)
        {
            var result = new List<Player>();
            var phrases = !specialPhrases.IsEmpty() ? specialPhrases : null;
            foreach ((int diffIndex, var handlers) in playerMapping)
                result.AddRange(difficulties[diffIndex].SetupPlayers(handlers.ToArray(), sync, phrases));
            return result.ToArray();
        }
    }

    public class InstrumentTrack<T> : InstrumentTrack_Base<DifficultyTrack<T>>
        where T : class, INote, new()
    {
    }
}
