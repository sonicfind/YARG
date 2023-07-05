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
    public static class DotChart_Scanner
    {
        public static bool Scan<T>(ref ScanValues scan, ChartFileReader reader)
            where T : IScannableFromDotChart, new()
        {
            T obj = new();
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE && obj.IsValid(reader.ExtractLaneAndSustain().Item1))
                {
                    scan.Set(index);
                    return false;
                }
                reader.NextEvent();
            }
            return true;
        }
    }
}
