﻿using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    
    public class FiveFret : GuitarNote, IReadableFromDotChart
    {
        public override int NumLanes => 6;
        public FiveFret() : base(6) { }

#nullable enable
        public override PlayableNote ConvertToPlayable(in long position, in SyncTrack sync, in long prevPosition, in INote? prevNote)
        {
            return new Playable_FiveFret(position, this, sync, prevPosition, prevNote as FiveFret);
        }

        public bool Set_From_Chart(uint lane, long length)
        {
            if (lane < 5)
            {
                lanes[lane + 1].Duration = length;
                lanes[0].Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                lanes[0].Duration = length;
                for (uint i = 1; i < 6; ++i)
                    lanes[i] = default;
            }
            else
                return false;
            return true;
        }
    }

    public class Playable_FiveFret : Playable_Guitar
    {
        public Playable_FiveFret(in long position, in FiveFret note, in SyncTrack sync, in long prevPosition, in FiveFret? prevNote)
            : base(position, note, sync, prevPosition, prevNote) { }

        public override void Draw(float trackPosition)
        {

        }
    }
}
