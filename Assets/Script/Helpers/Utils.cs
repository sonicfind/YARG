using System.Collections.Generic;
using UnityEngine;
using YARG.Chart;
using YARG.Song.Chart;
using YARG.Types;

namespace YARG.Util
{
    public static class Utils
    {
        /// <summary>
        /// Calculates the length of an Info(Note) object in beats.
        /// </summary>
        /// <param name="beatTimes">List of beat times associated with the Info object.</param>
        /// <returns>Length of the Info object in beats.</returns>
        public static float InfoLengthInBeats(Data.AbstractInfo info, FlatMap<BeatPosition, BeatStyle> beatTimes)
        {
            if (beatTimes.Count == 1)
                return 0;

            float prevBeat = beatTimes.At_index(0).key.seconds;
            float currBeat = beatTimes.At_index(1).key.seconds;

            int beatIndex = 1;

            bool Increment()
            {
                ++beatIndex;
                prevBeat = currBeat;

                if (beatIndex == beatTimes.Count)
                {
                    currBeat = default;
                    return false;
                }
                currBeat = beatTimes.At_index(beatIndex).key.seconds;
                return true;
            }

            // set beatIndex to first relevant beat
            while (currBeat <= info.time && Increment());

            float beats = 0;
            // add segments of the length wrt tempo
            if (beatIndex < beatTimes.Count)
            {
                while (currBeat <= info.EndTime)
                {
                    var curBPS = 1 / (currBeat - prevBeat);
                    beats += (currBeat - Mathf.Max(prevBeat, info.time)) * curBPS;

                    if (!Increment())
                        break;
                }
            }

            if (beatIndex < beatTimes.Count && prevBeat < info.EndTime && info.EndTime < currBeat)
            {
                var bps = 1 / (currBeat - prevBeat);
                beats += (info.EndTime - prevBeat) * bps;
            }
           

            return beats;
        }
    }
}