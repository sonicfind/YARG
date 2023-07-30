using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Serialization;
using YARG.Song.Entries.TrackScan;

namespace YARG.Song.Entries.DotChartValues.Drums
{
    public class Drums4_ProOutline : IScannableFromDotChart
    {
        public bool IsValid(nuint lane)
        {
            return lane < 5;
        }
    }

    public class Drum4_Pro_ChartScanner
    {
        public bool Scan(ref ScanValues scan, ref bool cymbals, ChartFileReader reader)
        {
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            bool validated = false;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    nuint lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane < 5)
                    {
                        if (!validated)
                        {
                            scan.Set(index);
                            validated = true;
                        }
                    }
                    else if (66 <= lane && lane <= 68 && !cymbals)
                    {
                        cymbals = true;
                        scan.Set(5);
                    }

                    if (validated && cymbals)
                        return false;
                }
                reader.NextEvent();
            }
            return true;
        }
    }
}
