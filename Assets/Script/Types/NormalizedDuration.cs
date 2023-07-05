using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Types
{
    public struct NormalizedDuration
    {
        private ulong _duration;
        public ulong Duration
        {
            get { return _duration; }
            set
            {
                if (value == 0)
                    value = 1;
                _duration = value;
            }
        }

        public NormalizedDuration(ulong duration)
        {
            _duration = 1;
            Duration = duration;
        }

        public static implicit operator ulong(NormalizedDuration dur) => dur._duration;
        public static implicit operator NormalizedDuration(ulong dur) => new(dur);
    }
}
