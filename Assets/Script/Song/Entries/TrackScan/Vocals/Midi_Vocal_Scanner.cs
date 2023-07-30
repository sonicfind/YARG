﻿using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan.Vocals
{
    public ref struct Midi_Vocal_Scanner
    {
        private bool percussion;
        private bool vocal;
        private bool lyric;
        private readonly int index;
        private MidiNote note;

        public Midi_Vocal_Scanner(int index)
        {
            this.index = index;
            percussion = false;
            vocal = false;
            lyric = false;
            note = default;
        }

        public bool Scan(MidiFileReader reader)
        {
            while (reader.TryParseEvent())
            {
                var ev = reader.GetEvent();
                if (ev.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0)
                    {
                        if (ParseNote())
                            return true;
                    }
                    else if (ParseNote_Off())
                        return true;
                }
                else if (ev.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref note);
                    if (ParseNote_Off())
                        return true;
                }
                else if (ev.type <= MidiEventType.Text_EnumLimit)
                {
                    var str = reader.ExtractTextOrSysEx();
                    if (str.Length != 0 && str[0] != '[')
                        lyric = true;
                }
            }
            return false;
        }

        private bool ParseNote()
        {
            if (36 <= note.value && note.value <= 84)
            {
                if (vocal && lyric)
                    return true;

                vocal = true;
                return false;
            }
            else if (index == 0 && (note.value == 96 || note.value == 97))
                percussion = true;
            return false;
        }

        private bool ParseNote_Off()
        {
            if (36 <= note.value && note.value <= 84)
                return vocal && lyric;
            else if (index == 0 && note.value == 96)
                return percussion;
            return false;
        }
    }
}
