using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries.TrackScan.Instrument.Drums
{
    public class LegacyDrumScan
    {
        private byte _validations;
        private DrumType _type;

        public bool cymbals;

        public byte ValidatedDiffs => _validations;
        public DrumType Type => _type;

        public LegacyDrumScan(bool cymbals, DrumType type = DrumType.UNKNOWN)
        {
            this.cymbals = cymbals;
            _type = type;
            _validations = 0;
        }

        public DrumType ScanMidi(MidiFileReader reader)
        {
            Midi_DrumLegacy_Scanner scanner = new(cymbals);
            _validations = scanner.Scan(reader);
            return scanner.Type;
        }

        public bool ScanDotChart(ChartFileReader reader)
        {
            int index = reader.Difficulty;
            int mask = 1 << index;
            if ((_validations & mask) > 0)
                return false;

            bool found = false;
            while (reader.IsStillCurrentTrack())
            {
                if (reader.ParseEvent().Item2 == ChartEvent.NOTE)
                {
                    int lane = reader.ExtractLaneAndSustain().Item1;
                    if (lane <= 5)
                    {
                        if (!found)
                        {
                            _validations |= (byte) mask;
                            found = true;
                        }

                        if (lane == 5)
                            _type = Types.DrumType.FIVE_LANE;
                    }
                    else if (66 <= lane && lane <= 68)
                    {
                        _type = Types.DrumType.FOUR_PRO;
                        cymbals = true;
                    }

                    if (found && Type != Types.DrumType.UNKNOWN)
                        return false;
                }
                reader.NextEvent();
            }
            return true;
        }
    }

    public class Midi_DrumLegacy_Scanner : Midi_Drum_Scanner_Base
    {
        private DrumType _type;
        private bool _cymbals;
        public DrumType Type { get { return _type; } }
        public bool Cymbals => _cymbals;

        public Midi_DrumLegacy_Scanner(bool forceProDrums)
        {
            _cymbals = forceProDrums;
        }

        protected override bool IsFullyScanned() { return validations == 31 && _type != DrumType.UNKNOWN; }
        protected override bool IsNote() { return 60 <= note.value && note.value <= 101; }

        protected override bool ParseLaneColor()
        {
            int noteValue = note.value - 60;
            int lane = LANEVALUES[noteValue];
            if (lane < 7)
            {
                notes[DIFFVALUES[noteValue], lane] = true;
                if (lane == 6)
                {
                    _type = Types.DrumType.FIVE_LANE;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        protected override bool ParseLaneColor_Off()
        {
            if (note.value < 60 || 101 < note.value)
                return false;

            int noteValue = note.value - 60;
            int diffIndex = DIFFVALUES[noteValue];
            if (!difficulties[diffIndex])
            {
                int lane = LANEVALUES[noteValue];
                if (lane < 7)
                {
                    Validate(diffIndex);
                    difficulties[diffIndex] = true;
                    return IsFullyScanned();
                }
            }
            return false;
        }

        protected override bool ToggleExtraValues()
        {
            if (110 <= note.value && note.value <= 112)
            {
                _type = Types.DrumType.FOUR_PRO;
                _cymbals = true;
                return IsFullyScanned();
            }
            return false;
        }
    }
}
