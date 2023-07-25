﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart.Notes
{
    
    public class FiveFret : GuitarNote, IReadableFromDotChart
    {
        public FiveFret() : base(6) { }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote)
        {
            (var type, var notes) = ConstructTypeAndNotes(position, prevPosition, prevNote as FiveFret);
            string mesh = type switch
            {
                PlayableGuitarType.STRUM => "FiveFret",
                PlayableGuitarType.HOPO => "FiveFretHopo",
                PlayableGuitarType.TAP => "FiveFretTap",
                _ => throw new Exception("stoopid")
            };
            return new PlayableNote_Guitar(mesh, type, notes);
        }

        public bool Set_From_Chart(uint lane, ulong length)
        {
            if (lane < 5)
            {
                lanes[lane + 1] = length;
                lanes[0].Disable();
            }
            else if (lane == 5)
                Forcing = ForceStatus.FORCED_LEGACY;
            else if (lane == 6)
                IsTap = true;
            else if (lane == 7)
            {
                lanes[0] = length;
                for (uint i = 1; i < 6; ++i)
                    lanes[i].Disable();
            }
            else
                return false;
            return true;
        }
    }
}
