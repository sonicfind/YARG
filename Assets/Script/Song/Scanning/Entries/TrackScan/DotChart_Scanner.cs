using YARG.Serialization;
using YARG.Song.Entries.DotChartValues;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan
{
    public static class DotChart_Scanner<T>
        where T : class, IScannableFromDotChart, new()
    {
        private static readonly T obj = new();
        public static bool Scan(ref ScanValues scan, ChartFileReader reader)
        {
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    if (obj.IsValid(reader.ExtractLaneAndSustain().Item1))
                    {
                        scan.Set(index);
                        return false;
                    }
                }
                reader.NextEvent();
            }
            return true;
        }
    }
}
