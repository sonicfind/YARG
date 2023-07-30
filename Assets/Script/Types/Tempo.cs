using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Types
{
    public struct Tempo
    {
        public const int BPM_FACTOR = 60000000;
        public const int DEFAULT_BPM = 120;
        public const int MICROS_AT_120BPM = BPM_FACTOR / DEFAULT_BPM;

        private int _micros;
        public int Micros
        {
            get { return _micros; }
            set { _micros = value; }
        }

        public float BPM
        {
            get { return _micros != 0 ? (float)BPM_FACTOR / _micros : 0; }
            set { _micros = value != 0 ? (int)(BPM_FACTOR / value) : 0; }
        }

        public long Anchor { get; set; }
        public Tempo(int micros) { _micros = micros; Anchor = 0; }
    }
}
