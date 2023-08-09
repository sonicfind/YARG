using YARG.Serialization;
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
        public static bool Scan(ChartFileReader reader, ref ScanValues scan, Func<int, bool> func)
        {
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    if (func(reader.ExtractLaneAndSustain().Item1))
                    {
                        scan.Set(index);
                        return false;
                    }
                }
                reader.NextEvent();
            }
            return true;
        }

        public static bool ValidateKeys(int lane)
        {
            return lane < 5;
        }

        public static bool ValidateSixFret(int lane)
        {
            return lane < 5 || lane == 8 || lane == 7;
        }

        public static bool ValidateFiveFret(int lane)
        {
            return lane < 5 || lane == 7;
        }

        public static bool ValidateFiveLaneDrums(int lane)
        {
            return lane < 6;
        }

        public static bool ValidateFourLaneProDrums(int lane)
        {
            return lane < 5;
        }

        public static bool ScanFourLaneDrums_ValidateCymbals(ChartFileReader reader, ref ScanValues scan, ref bool cymbals)
        {
            int index = reader.Difficulty;
            if (scan[index])
                return false;

            bool validated = false;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
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
