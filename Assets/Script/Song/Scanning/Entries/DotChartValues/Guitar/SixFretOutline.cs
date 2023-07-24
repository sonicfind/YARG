﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.DotChartValues.Guitar
{
    public class SixFretOutline : IScannableFromDotChart
    {
        public bool IsValid(nuint lane)
        {
            return lane < 5 || lane == 8 || lane == 7;
        }
    }
}
