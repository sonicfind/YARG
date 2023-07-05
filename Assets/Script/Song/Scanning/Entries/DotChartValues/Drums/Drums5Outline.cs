using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.DotChartValues.Drums
{
    public class Drums5Outline : IScannableFromDotChart
    {
        public bool IsValid(nuint lane)
        {
            return lane < 6;
        }
    }
}
