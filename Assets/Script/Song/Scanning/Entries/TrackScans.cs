using YARG.Serialization;
using YARG.Song.Entries.DotChartValues.Drums;
using YARG.Song.Entries.DotChartValues.Guitar;
using YARG.Song.Entries.DotChartValues.Keys;
using YARG.Song.Entries.TrackScan;
using YARG.Song.Entries.TrackScan.Instrument.Drums;
using YARG.Song.Entries.TrackScan.Instrument.Guitar;
using YARG.Song.Entries.TrackScan.Instrument.Keys;
using YARG.Song.Entries.TrackScan.Instrument.ProGuitar;
using YARG.Song.Entries.TrackScan.Instrument.ProKeys;
using YARG.Song.Entries.TrackScan.Vocals;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries
{
    public struct TrackScans
    {
        public ScanValues lead_5;
        public ScanValues lead_6;
        public ScanValues bass_5;
        public ScanValues bass_6;
        public ScanValues rhythm;
        public ScanValues coop;
        public ScanValues keys;
        public ScanValues drums_4;
        public ScanValues drums_4pro;
        public ScanValues drums_5;
        public ScanValues proguitar_17;
        public ScanValues proguitar_22;
        public ScanValues probass_17;
        public ScanValues probass_22;
        public ScanValues proKeys;

        public ScanValues leadVocals;
        public ScanValues harmonyVocals;
        public TrackScans(int _)
        {
            lead_5 = new();
            lead_6 = new();
            bass_5= new();
            bass_6 = new();
            rhythm = new();
            coop = new();
            keys = new();
            drums_4 = new();
            drums_4pro = new();
            drums_5 = new();
            proguitar_17 = new();
            proguitar_22 = new();
            probass_17= new();
            probass_22= new();
            proKeys= new();
            leadVocals = new();
            harmonyVocals = new();
        }

        public TrackScans(BinaryFileReader reader)
        {
            lead_5 = default;
            lead_6 = default;
            bass_5 = default;
            bass_6 = default;
            rhythm = default;
            coop = default;
            keys = default;
            drums_4 = default;
            drums_4pro = default;
            drums_5 = default;
            proguitar_17 = default;
            proguitar_22 = default;
            probass_17 = default;
            probass_22 = default;
            proKeys = default;
            leadVocals = default;
            harmonyVocals = default;
            unsafe
            {
                fixed (ScanValues* scans = &lead_5)
                    reader.CopyTo((byte*) scans, 17 * sizeof(ScanValues));
            }
        }

        public bool CheckForValidScans()
        {
            return lead_5.subTracks > 0     || bass_5.subTracks > 0        || keys.subTracks > 0         || drums_4pro.subTracks > 0 ||
                   leadVocals.subTracks > 0 || harmonyVocals.subTracks > 0 || proguitar_17.subTracks > 0 || proguitar_22.subTracks > 0 ||
                   probass_17.subTracks > 0 || probass_22.subTracks > 0    || proKeys.subTracks > 0      || rhythm.subTracks > 0 ||
                   coop.subTracks > 0       || drums_5.subTracks > 0       || lead_6.subTracks > 0       || bass_6.subTracks > 0;
        }

        public void ScanFromMidi(MidiTrackType trackType, Types.DrumType drumType, MidiFileReader reader)
        {
            switch (trackType)
            {
                case MidiTrackType.Guitar_5:
                    {
                        if (lead_5.subTracks == 0)
                            lead_5 = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Bass_5:
                    {
                        if (bass_5.subTracks == 0)
                            bass_5 = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Keys:
                    {
                        if (keys.subTracks == 0)
                            keys = new Midi_Keys_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Drums:
                    {
                        if (drumType == Types.DrumType.FOUR_PRO)
                        {
                            if (drums_4pro.subTracks == 0)
                                drums_4pro = new Midi_Drum4Pro_Scanner().Scan(reader);
                        }
                        else if (drumType == Types.DrumType.FIVE_LANE)
                        {
                            if (drums_5.subTracks == 0)
                                drums_5 = new Midi_Drum5_Scanner().Scan(reader);
                        }
                        else
                        {
                            LegacyDrumScan legacy = new();
                            if (legacy.ScanMidi(reader) == Types.DrumType.FIVE_LANE)
                            {
                                if (drums_5.subTracks == 0)
                                    drums_5 = legacy.Values;
                            }
                            else if (drums_4pro.subTracks == 0)
                                drums_4pro = legacy.Values;
                        }
                        break;
                    }
                case MidiTrackType.Vocals:
                    {
                        if (!leadVocals[0] && new Midi_Vocal_Scanner(0).Scan(reader))
                            leadVocals.Set(0);
                        break;
                    }
                case MidiTrackType.Harm1:
                    {
                        if (!harmonyVocals[0] && new Midi_Vocal_Scanner(0).Scan(reader))
                            harmonyVocals.Set(0);
                        break;
                    }
                case MidiTrackType.Harm2:
                    {
                        if (!harmonyVocals[1] && new Midi_Vocal_Scanner(0).Scan(reader))
                            harmonyVocals.Set(1);
                        break;
                    }
                case MidiTrackType.Harm3:
                    {
                        if (!harmonyVocals[2] && new Midi_Vocal_Scanner(0).Scan(reader))
                            harmonyVocals.Set(2);
                        break;
                    }
                case MidiTrackType.Rhythm:
                    {
                        if (rhythm.subTracks == 0)
                            rhythm = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Coop:
                    {
                        if (coop.subTracks == 0)
                            coop = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Guitar:
                    {
                        if (proguitar_17.subTracks == 0)
                            proguitar_17 = new Midi_ProGuitar17_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Guitar_22:
                    {
                        if (proguitar_22.subTracks == 0)
                            proguitar_22 = new Midi_ProGuitar22_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Bass:
                    {
                        if (probass_17.subTracks == 0)
                            probass_17 = new Midi_ProGuitar17_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Bass_22:
                    {
                        if (probass_22.subTracks == 0)
                            probass_22 = new Midi_ProGuitar22_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_X:
                    {
                        if (!proKeys[3])
                            proKeys |= new Midi_ProKeys_Scanner(3).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_H:
                    {
                        if (!proKeys[2])
                            proKeys |= new Midi_ProKeys_Scanner(2).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_M:
                    {
                        if (!proKeys[1])
                            proKeys |= new Midi_ProKeys_Scanner(1).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_E:
                    {
                        if (!proKeys[0])
                            proKeys |= new Midi_ProKeys_Scanner(0).Scan(reader);
                        break;
                    }
                case MidiTrackType.Guitar_6:
                    {
                        if (lead_6.subTracks == 0)
                            lead_6 = new Midi_SixFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Bass_6:
                    {
                        if (bass_6.subTracks == 0)
                            bass_6 = new Midi_SixFret_Scanner().Scan(reader);
                        break;
                    }
            }
        }

        public bool ScanFromDotChart(ref LegacyDrumScan legacy, ChartFileReader reader)
        {
            switch (reader.Instrument)
            {
                case NoteTracks_Chart.Single:       return DotChart_Scanner.Scan<FiveFretOutline>(ref lead_5, reader);
                case NoteTracks_Chart.DoubleGuitar: return DotChart_Scanner.Scan<FiveFretOutline>(ref coop, reader);
                case NoteTracks_Chart.DoubleBass:   return DotChart_Scanner.Scan<FiveFretOutline>(ref bass_5, reader);
                case NoteTracks_Chart.DoubleRhythm: return DotChart_Scanner.Scan<FiveFretOutline>(ref rhythm, reader);
                case NoteTracks_Chart.Drums:
                    {
                        switch (legacy.Type)
                        {
                            case Types.DrumType.FOUR_PRO:  return DotChart_Scanner.Scan<Drums4_ProOutline>(ref drums_4pro, reader);
                            case Types.DrumType.FIVE_LANE: return DotChart_Scanner.Scan<Drums5Outline>(ref drums_5, reader);
                            case Types.DrumType.UNKNOWN:   return legacy.ScanDotChart(reader);
                        }
                        break;
                    }
                case NoteTracks_Chart.Keys:      return DotChart_Scanner.Scan<KeysOutline>(ref keys, reader);
                case NoteTracks_Chart.GHLGuitar: return DotChart_Scanner.Scan<SixFretOutline>(ref lead_6, reader);
                case NoteTracks_Chart.GHLBass:   return DotChart_Scanner.Scan<SixFretOutline>(ref bass_6, reader);
            }
            return true;
        }

        public void Update(TrackScans update)
        {
            unsafe
            {
                fixed (ScanValues* scans = &lead_5)
                {
                    var updatedScans = &update.lead_5;
                    for (int i = 0; i < 17; ++i)
                        if (updatedScans[i].subTracks > 0)
                            scans[i] = updatedScans[i];
                }
            }
        }

        public void WriteToCache(BinaryWriter writer)
        {
            unsafe
            {
                fixed (ScanValues* scans = &lead_5)
                {
                    byte* yay = (byte*)scans;
                    writer.Write(new ReadOnlySpan<byte>(yay, 17 * sizeof(ScanValues)));
                }
            }
        }
    }
}
