using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace YARG.Data
{
    [JsonObject(MemberSerialization.Fields)]
    public class SongScore
    {
        public DateTime lastPlayed;
        public int timesPlayed;

        public Dictionary<Instrument, DiffPercent> highestPercent;

        public Dictionary<Instrument, DiffScore> highestScore;

        public KeyValuePair<Instrument, DiffPercent> GetHighestPercent()
        {
            KeyValuePair<Instrument, DiffPercent> highest = default;

            foreach (var kvp in highestPercent)
            {
                if (kvp.Value > highest.Value)
                {
                    highest = kvp;
                }
            }

            return highest;
        }

        public KeyValuePair<Instrument, DiffScore> GetHighestScore()
        {
            KeyValuePair<Instrument, DiffScore> highest = default;

            foreach (var kvp in highestScore)
            {
                if (kvp.Value > highest.Value)
                {
                    highest = kvp;
                }
            }

            return highest;
        }
    }
}