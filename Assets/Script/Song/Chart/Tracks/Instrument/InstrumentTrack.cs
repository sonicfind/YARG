using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public override ulong GetLastNoteTime()
        {
            ulong endTime = 0;
            for (int i = 0; i < difficulties.Length; ++i)
            {
                ulong end = difficulties[i].GetLastNoteTime();
                if (end > endTime)
                    endTime = end;
            }
            return endTime;
        }

        public Player_Instrument[] SetupPlayers(Dictionary<int, InputHandler[]> playerMapping)
        {
            var result = new List<Player_Instrument>();
            var phrases = !specialPhrases.IsEmpty() ? specialPhrases : null;
            foreach ((int diffIndex, var handlers) in playerMapping)
                result.AddRange(difficulties[diffIndex].SetupPlayers(handlers, phrases));
            return result.ToArray();
        }
    }

    public class InstrumentTrack<T> : InstrumentTrack_Base<DifficultyTrack<T>>
        where T : class, INote, new()
    {
    }
}
