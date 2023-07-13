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
    public class TrackScans
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
        public TrackScans()
        {
            lead_5 =        new(-1);
            lead_6 =        new(-1);
            bass_5=         new(-1);
            bass_6 =        new(-1);
            rhythm =        new(-1);
            coop =          new(-1);
            keys =          new(-1);
            drums_4 =       new(-1);
            drums_4pro =    new(-1);
            drums_5 =       new(-1);
            proguitar_17 =  new(-1);
            proguitar_22 =  new(-1);
            probass_17 =    new(-1);
            probass_22 =    new(-1);
            proKeys =       new(-1);
            leadVocals =    new(-1);
            harmonyVocals = new(-1);
        }

        public TrackScans(BinaryFileReader reader)
        {
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

        public void ScanFromMidi(MidiTrackType trackType, DrumType drumType, ref bool forceProDrums, MidiFileReader reader)
        {
            switch (trackType)
            {
                case MidiTrackType.Guitar_5:
                    {
                        if (lead_5.subTracks == 0)
                            lead_5.subTracks = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Bass_5:
                    {
                        if (bass_5.subTracks == 0)
                            bass_5.subTracks = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Keys:
                    {
                        if (keys.subTracks == 0)
                            keys.subTracks = new Midi_Keys_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Drums:
                    {
                        if (drumType == DrumType.FOUR_PRO)
                        {
                            if (drums_4pro.subTracks == 0)
                            {
                                Midi_Drum4Pro_Scanner scanner = new(forceProDrums);
                                drums_4pro.subTracks = scanner.Scan(reader);
                                forceProDrums = scanner.Cymbals;
                            }
                        }
                        else if (drumType == DrumType.FIVE_LANE)
                        {
                            if (drums_5.subTracks == 0)
                                drums_5.subTracks = new Midi_Drum5_Scanner().Scan(reader);
                        }
                        else
                        {
                            LegacyDrumScan legacy = new(forceProDrums);
                            if (legacy.ScanMidi(reader) == DrumType.FIVE_LANE)
                            {
                                if (drums_5.subTracks == 0)
                                {
                                    drums_5.subTracks = legacy.ValidatedDiffs;
                                    forceProDrums = legacy.cymbals;
                                }
                            }
                            else if (drums_4pro.subTracks == 0)
                            {
                                drums_4pro.subTracks = legacy.ValidatedDiffs;
                                forceProDrums = legacy.cymbals;
                            }
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
                            rhythm.subTracks = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Coop:
                    {
                        if (coop.subTracks == 0)
                            coop.subTracks = new Midi_FiveFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Guitar:
                    {
                        if (proguitar_17.subTracks == 0)
                            proguitar_17.subTracks = new Midi_ProGuitar17_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Guitar_22:
                    {
                        if (proguitar_22.subTracks == 0)
                            proguitar_22.subTracks = new Midi_ProGuitar22_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Bass:
                    {
                        if (probass_17.subTracks == 0)
                            probass_17.subTracks = new Midi_ProGuitar17_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Bass_22:
                    {
                        if (probass_22.subTracks == 0)
                            probass_22.subTracks = new Midi_ProGuitar22_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_X:
                    {
                        if (!proKeys[3])
                            proKeys.subTracks |= new Midi_ProKeys_Scanner(3).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_H:
                    {
                        if (!proKeys[2])
                            proKeys.subTracks |= new Midi_ProKeys_Scanner(2).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_M:
                    {
                        if (!proKeys[1])
                            proKeys.subTracks |= new Midi_ProKeys_Scanner(1).Scan(reader);
                        break;
                    }
                case MidiTrackType.Real_Keys_E:
                    {
                        if (!proKeys[0])
                            proKeys.subTracks |= new Midi_ProKeys_Scanner(0).Scan(reader);
                        break;
                    }
                case MidiTrackType.Guitar_6:
                    {
                        if (lead_6.subTracks == 0)
                            lead_6.subTracks = new Midi_SixFret_Scanner().Scan(reader);
                        break;
                    }
                case MidiTrackType.Bass_6:
                    {
                        if (bass_6.subTracks == 0)
                            bass_6.subTracks = new Midi_SixFret_Scanner().Scan(reader);
                        break;
                    }
            }
        }

        public bool ScanFromDotChart(ref LegacyDrumScan legacy, ChartFileReader reader)
        {
            switch (reader.Instrument)
            {
                case NoteTracks_Chart.Single:       return DotChart_Scanner<FiveFretOutline>.Scan(ref lead_5, reader);
                case NoteTracks_Chart.DoubleGuitar: return DotChart_Scanner<FiveFretOutline>.Scan(ref coop, reader);
                case NoteTracks_Chart.DoubleBass:   return DotChart_Scanner<FiveFretOutline>.Scan(ref bass_5, reader);
                case NoteTracks_Chart.DoubleRhythm: return DotChart_Scanner<FiveFretOutline>.Scan(ref rhythm, reader);
                case NoteTracks_Chart.Drums:
                    {
                        switch (legacy.Type)
                        {
                            case DrumType.FOUR_PRO:
                                {
                                    if (legacy.cymbals)
                                        return DotChart_Scanner<Drums4_ProOutline>.Scan(ref drums_4pro, reader);
                                    return new Drum4_Pro_ChartScanner().Scan(ref drums_4pro, ref legacy.cymbals, reader);
                                }
                            case DrumType.FIVE_LANE: return DotChart_Scanner<Drums5Outline>.Scan(ref drums_5, reader);
                            case DrumType.UNKNOWN:   return legacy.ScanDotChart(reader);
                        }
                        break;
                    }
                case NoteTracks_Chart.Keys:      return DotChart_Scanner<KeysOutline>.Scan(ref keys, reader);
                case NoteTracks_Chart.GHLGuitar: return DotChart_Scanner<SixFretOutline>.Scan(ref lead_6, reader);
                case NoteTracks_Chart.GHLBass:   return DotChart_Scanner<SixFretOutline>.Scan(ref bass_6, reader);
            }
            return true;
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
