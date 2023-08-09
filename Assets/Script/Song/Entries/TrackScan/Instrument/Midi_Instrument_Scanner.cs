using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan.Instrument
{
    public abstract class Midi_Instrument_Scanner_Base
    {
        protected static readonly byte[] SYSEXTAG = Encoding.ASCII.GetBytes("PS");
        protected static readonly int[] ValidationMask = { 1, 2, 4, 8, 16 };
        protected MidiParseEvent currEvent;
        protected int validations;
        protected MidiNote note;

        public byte Scan(MidiFileReader reader)
        {
            while (reader.TryParseEvent(ref currEvent))
            {
                if (currEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0)
                    {
                        if (ParseNote())
                            break;
                    }
                    else if (ParseNote_Off())
                        break;
                }
                else if (currEvent.type == MidiEventType.Note_Off)
                {
                    reader.ExtractMidiNote(ref note);
                    if (ParseNote_Off())
                        break;
                }
                else if (currEvent.type == MidiEventType.SysEx || currEvent.type == MidiEventType.SysEx_End)
                    ParseSysEx(reader.ExtractTextOrSysEx());
                else if (currEvent.type <= MidiEventType.Text_EnumLimit)
                    ParseText(reader.ExtractTextOrSysEx());
            }
            return (byte)validations;
        }

        private bool ParseNote()
        {
            if (ProcessSpecialNote())
                return false;

            if (IsNote())
                return ParseLaneColor();
            return ToggleExtraValues();
        }

        private bool ParseNote_Off()
        {
            return ProcessSpecialNote_Off() || ParseLaneColor_Off();
        }

        protected abstract bool ParseLaneColor();

        protected abstract bool ParseLaneColor_Off();

        protected virtual bool IsFullyScanned() { return validations == 15; }

        protected virtual void ParseText(ReadOnlySpan<byte> str) {}

        protected virtual bool IsNote() { return 60 <= note.value && note.value <= 100; }

        protected virtual bool ProcessSpecialNote() { return false; }

        protected virtual bool ProcessSpecialNote_Off() { return false; }

        protected virtual bool ToggleExtraValues() { return false; }

        protected virtual void ParseSysEx(ReadOnlySpan<byte> str) { }

        protected void Validate(int diffIndex) { validations |= ValidationMask[diffIndex]; }
    }

    public abstract class Midi_Instrument_Scanner : Midi_Instrument_Scanner_Base
    {
        internal static readonly int[] DIFFVALUES = {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
            2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3
        };
        protected bool[] difficulties = new bool[4];
    }
}
