using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public class Arpeggio<FretType>
        where FretType : unmanaged, IFretted
    {
        private NormalizedDuration _length = new(1);
        public readonly FretType[] strings = new FretType[6];

        public long Length
        {
            get { return _length; }
            set { _length = value; }
        }
    }
}
