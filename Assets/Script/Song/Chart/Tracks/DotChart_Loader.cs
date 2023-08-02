using YARG.Serialization;
using YARG.Song.Chart;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Song.Chart.Notes;

namespace YARG.Song.Chart
{
    public static class DotChart_Loader
    {
        internal static readonly byte[] SOLO = Encoding.ASCII.GetBytes("solo");
        internal static readonly byte[] SOLOEND = Encoding.ASCII.GetBytes("soloend");

        public static bool Load<T>(ref DifficultyTrack<T> diff, ChartFileReader reader)
            where T : class, INote, IReadableFromDotChart, new()
        {
            if (diff.IsOccupied())
                return false;

            long solo = 0;
            diff.notes.Capacity = 5000;
            while (reader.IsStillCurrentTrack())
            {
                var trackEvent = reader.ParseEvent();
                switch (trackEvent.Item2)
                {
                case ChartEvent.NOTE:
                {
                    var note = reader.ExtractLaneAndSustain();
                    if (!diff.notes.Get_Or_Add_Back(trackEvent.Item1).Set_From_Chart(note.Item1, note.Item2))
                        if (!diff.notes.Last().HasActiveNotes())
                            diff.notes.Pop();
                    break;
                }
                case ChartEvent.SPECIAL:
                {
                    var phrase = reader.ExtractSpecialPhrase();
                    switch (phrase.Type)
                    {
                    case SpecialPhraseType.StarPower:
                    case SpecialPhraseType.BRE:
                    case SpecialPhraseType.Tremolo:
                    case SpecialPhraseType.Trill:
                        diff.specialPhrases.Get_Or_Add_Back(trackEvent.Item1).Add(phrase);
                        break;
                    }
                    break;
                }
                case ChartEvent.EVENT:
                {
                    var str = reader.ExtractTextAsBytes();
                    if (str.StartsWith(SOLOEND))
                        diff.specialPhrases[solo].Add(new(SpecialPhraseType.Solo, trackEvent.Item1 - solo));
                    else if (str.StartsWith(SOLO))
                        solo = trackEvent.Item1;
                    else
                        diff.events.Get_Or_Add_Back(trackEvent.Item1).Add(Encoding.UTF8.GetString(str));
                    break;
                }
                }
                reader.NextEvent();
            }
            diff.TrimExcess();
            return true;
        }
    }
}
