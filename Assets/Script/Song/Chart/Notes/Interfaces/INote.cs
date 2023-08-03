using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Assets.Script.Types;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public interface INote
    {
        public int NumLanes { get; }
        public bool HasActiveNotes();
        public long GetLongestSustain();
#nullable enable
        public PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote);
    }
}
