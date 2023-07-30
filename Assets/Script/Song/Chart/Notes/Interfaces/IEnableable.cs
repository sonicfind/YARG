using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    public interface IEnableable
    {
        public long Duration { get; set; }
        public bool IsActive();
        public void Disable();
    }
}
