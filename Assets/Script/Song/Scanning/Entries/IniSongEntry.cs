﻿using YARG.Ini;
using YARG.Library.CacheNodes;
using YARG.Modifiers;
using YARG.Serialization;
using YARG.Song.Entries.TrackScan.Instrument.Drums;
using YARG.Types;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Song.Entries
{
    public class IniSongEntry : SongEntry
    {
        private const string DEFAULT_NAME = "Unknown Title";
        

        private static readonly Dictionary<string, ModifierNode> MODIFIER_LIST = new()
        {
            { "Album",        new("album", ModifierNodeType.STRING_CHART ) },
            { "Artist",       new("artist", ModifierNodeType.STRING_CHART ) },
            { "Charter",      new("charter", ModifierNodeType.STRING_CHART ) },
            { "Difficulty",   new("diff_band", ModifierNodeType.INT32 ) },
            { "Genre",        new("genre", ModifierNodeType.STRING_CHART ) },
            { "Name",         new("name", ModifierNodeType.STRING_CHART ) },
            { "PreviewEnd",   new("preview_end_time", ModifierNodeType.FLOAT ) },
            { "PreviewStart", new("preview_start_time", ModifierNodeType.FLOAT ) },
            { "Year",         new("year", ModifierNodeType.STRING_CHART ) },
        };

        static IniSongEntry() { }

        private ulong m_sustain_cutoff_threshold = 0;
        private ushort m_hopofreq_Old = ushort.MaxValue;
        private bool m_eighthnote_hopo = false;
        private byte m_multiplier_note = 116;

        public ushort Hopofreq_Old => m_hopofreq_Old;
        public byte MultiplierNote => m_multiplier_note;
        public ulong SustainCutoffThreshold => m_sustain_cutoff_threshold;
        public bool EightNoteHopo => m_eighthnote_hopo;

        private SortString m_directory_playlist;

        private Dictionary<string, List<Modifier>> m_modifiers = new();

        private (string, ChartType) m_chartType;

#nullable enable
        private readonly FileInfo? m_chartFile;
        private readonly FileInfo? m_iniFile;

        public IniSongEntry(FrameworkFile file, FileInfo chartFile, FileInfo? iniFile, ref (string, ChartType) type)
        {
            if (iniFile != null)
            {
                m_modifiers = IniHandler.ReadSongIniFile(iniFile.FullName);
                m_iniFile = iniFile;
            }

            if (type.Item2 == ChartType.CHART)
                Scan_Chart(file);

            if (GetModifier("name") == null)
                return;

            if (type.Item2 == ChartType.MID || type.Item2 == ChartType.MIDI)
                Scan_Midi(file);

            if (!m_scans.CheckForValidScans())
                return;

            m_chartType = type;
            m_chartFile = chartFile;
            Directory = chartFile.DirectoryName!;
            m_directory_playlist.Str = Path.GetDirectoryName(Directory)!;
        }

        public IniSongEntry(string directory, FileInfo chartFile, FileInfo? iniFile, ref (string, ChartType) type, BinaryFileReader reader, CategoryCacheStrings strings) : base(reader, strings)
        {
            Directory = directory;
            m_chartType = type;
            m_chartFile = chartFile;
            m_iniFile = iniFile;

            m_sustain_cutoff_threshold = reader.ReadUInt64();
            m_hopofreq_Old = reader.ReadUInt16();
            m_eighthnote_hopo = reader.ReadBoolean();
            m_multiplier_note = reader.ReadByte();
        }

        public bool ScannedSuccessfully() { return m_chartFile != null; }

        public Modifier? GetModifier(string name)
        {
            if (m_modifiers.TryGetValue(name, out var nameMods))
                return nameMods[0];
            return null;
        }

        public void FinishScan()
        {
            if (m_modifiers.Count > 0)
                MapModifierVariables();
        }

        private void Scan_Chart(FrameworkFile file)
        {
            using ChartFileReader reader = new(file);
            if (!reader.ValidateHeaderTrack())
                throw new System.Exception("[Song] track expected at the start of the file");

            foreach (var node in reader.ExtractModifiers(MODIFIER_LIST))
            {
                if (m_modifiers.TryGetValue(node.Key, out var modifiers))
                    modifiers.AddRange(node.Value);
                else
                    m_modifiers.Add(node.Key, modifiers!);
            }

            LegacyDrumScan legacy = new(GetDrumTypeFromModifier());
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument() || !m_scans.ScanFromDotChart(ref legacy, reader))
                    reader.SkipTrack();
            }

            if (legacy.Type == Types.DrumType.FIVE_LANE)
                m_scans.drums_5 |= legacy.Values;
            else
                m_scans.drums_4pro |= legacy.Values;
        }

        private void Scan_Midi(FrameworkFile file)
        {
            using MidiFileReader reader = new(file);
            var drumType = GetDrumTypeFromModifier();
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events && type != MidiTrackType.Beats)
                        m_scans.ScanFromMidi(type, drumType, reader);
                }
            }
        }

        private void MapModifierVariables()
        {
            if (m_modifiers.TryGetValue("name", out var names))
            {
                for (int i = 0; i < names.Count; ++i)
                {
                    m_name = names[i].SORTSTR;
                    if (m_name.Str != DEFAULT_NAME)
                        break;
                }
            }

            if (m_modifiers.TryGetValue("artist", out var artists))
            {
                for (int i = 0; i < artists.Count; ++i)
                {
                    m_artist = artists[i].SORTSTR;
                    if (m_artist.Str != s_DEFAULT_ARTIST.Str)
                        break;
                }
            }
            else
                m_artist = s_DEFAULT_ARTIST;

            if (m_modifiers.TryGetValue("album", out var albums))
            {
                for (int i = 0; i < albums.Count; ++i)
                {
                    m_album = albums[i].SORTSTR;
                    if (m_album.Str != s_DEFAULT_ALBUM.Str)
                        break;
                }
            }
            else
                m_album = s_DEFAULT_ALBUM;

            if (m_modifiers.TryGetValue("genre", out var genres))
            {
                for (int i = 0; i < genres.Count; ++i)
                {
                    m_genre = genres[i].SORTSTR;
                    if (m_genre.Str != s_DEFAULT_GENRE.Str)
                        break;
                }
            }
            else
                m_genre = s_DEFAULT_GENRE;

            if (m_modifiers.TryGetValue("year", out var years))
            {
                for (int i = 0; i < years.Count; ++i)
                {
                    m_year = years[i].SORTSTR;
                    if (m_year.Str != s_DEFAULT_YEAR.Str)
                        break;
                }
            }
            else
                m_year = s_DEFAULT_YEAR;

            if (m_modifiers.TryGetValue("charter", out var charters))
            {
                for (int i = 0; i < charters.Count; ++i)
                {
                    m_charter = charters[i].SORTSTR;
                    if (m_charter.Str != s_DEFAULT_CHARTER.Str)
                        break;
                }
            }
            else
                m_charter = s_DEFAULT_CHARTER;

            if (m_modifiers.TryGetValue("playlist", out var playlists))
            {
                for (int i = 0; i < playlists.Count; ++i)
                {
                    m_playlist = playlists[i].SORTSTR;
                    if (m_playlist.Str != m_directory_playlist.Str)
                        break;
                }
            }
            else
                m_playlist = m_directory_playlist;

            if (m_modifiers.TryGetValue("source", out var sources))
            {
                for (int i = 0; i < sources.Count; ++i)
                {
                    m_source = sources[i].SORTSTR;
                    if (m_source.Str != s_DEFAULT_SOURCE.Str)
                        break;
                }
            }
            else
                m_source = s_DEFAULT_SOURCE;

            if (m_modifiers.TryGetValue("song_length", out var songLengths))
                m_song_length = songLengths[0].UINT64;

            if (m_modifiers.TryGetValue("preview_start_time", out var preview_start))
                m_previewStart = preview_start[0].FLOAT;

            if (m_modifiers.TryGetValue("preview_end_time", out var preview_end))
                m_previewEnd = preview_end[0].FLOAT;

            if (m_modifiers.TryGetValue("album_track", out var album_track))
                m_album_track = album_track[0].UINT16;

            if (m_modifiers.TryGetValue("playlist_track", out var playlist_track))
                m_playlist_track = playlist_track[0].UINT16;

            if (m_modifiers.TryGetValue("icon", out var icon))
                m_icon = icon[0].STR;

            if (m_modifiers.TryGetValue("hopo_frequency", out var hopo_freq))
                m_hopo_frequency = hopo_freq[0].UINT64;

            if (m_modifiers.TryGetValue("multiplier_note", out var multiplier))
                if (multiplier[0].UINT16 == 103)
                    m_multiplier_note = 103;

            if (m_modifiers.TryGetValue("eighthnote_hopo", out var eighthnote))
                m_eighthnote_hopo = eighthnote[0].BOOL;

            if (m_modifiers.TryGetValue("sustain_cutoff_threshold", out var threshold))
                m_sustain_cutoff_threshold = threshold[0].UINT64;

            if (m_modifiers.TryGetValue("hopofreq", out var hopofreq_old))
                m_hopofreq_Old = hopofreq_old[0].UINT16;

            {
                if (m_modifiers.TryGetValue("diff_band", out var intensities))
                    m_bandIntensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitar", out var intensities))
                    m_scans.lead_5.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitarghl", out var intensities))
                    m_scans.lead_6.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass", out var intensities))
                    m_scans.bass_5.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bassghl", out var intensities))
                    m_scans.bass_6.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_rhythm", out var intensities))
                    m_scans.rhythm.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitar_coop", out var intensities))
                    m_scans.coop.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_keys", out var intensities))
                    m_scans.keys.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_drums", out var intensities))
                {
                    sbyte intensity = (sbyte)intensities[0].INT32;
                    m_scans.drums_4.intensity = intensity;
                    m_scans.drums_4pro.intensity = intensity;
                    m_scans.drums_5.intensity = intensity;
                }
            }

            {
                if (m_modifiers.TryGetValue("diff_drums_real", out var intensities))
                    m_scans.drums_4pro.intensity = (sbyte)intensities[0].INT32;
            }

            m_scans.drums_4 = m_scans.drums_4pro;

            {
                if (m_modifiers.TryGetValue("pro_drums", out var proDrums) && !proDrums[0].BOOL)
                    m_scans.drums_4pro.subTracks = 0;
            }


            {
                if (m_modifiers.TryGetValue("diff_guitar_real", out var intensities))
                    m_scans.proguitar_17.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_guitar_real_22", out var intensities))
                    m_scans.proguitar_22.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass_real", out var intensities))
                    m_scans.probass_17.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_bass_real_22", out var intensities))
                    m_scans.probass_22.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_vocals", out var intensities))
                    m_scans.leadVocals.intensity = (sbyte)intensities[0].INT32;
            }

            {
                if (m_modifiers.TryGetValue("diff_vocals_harm", out var intensities))
                    m_scans.harmonyVocals.intensity = (sbyte)intensities[0].INT32;
            }

            if (m_scans.harmonyVocals.subTracks > 0)
                VocalParts = 3;
            else if (m_scans.leadVocals.subTracks > 0)
                VocalParts = 1;
        }

        public byte[] FormatCacheData(CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Directory);
            writer.Write((byte)m_chartType.Item2);
            writer.Write(m_chartFile!.LastWriteTime.ToBinary());
            writer.Write(m_iniFile != null);
            if (m_iniFile != null)
                writer.Write(m_iniFile.LastWriteTime.ToBinary());

            FormatCacheData(writer, node);

            writer.Write(m_sustain_cutoff_threshold);
            writer.Write(m_hopofreq_Old);
            writer.Write(m_eighthnote_hopo);
            writer.Write(m_multiplier_note);
            return ms.ToArray();
        }

        private Types.DrumType GetDrumTypeFromModifier()
        {
            if (m_modifiers.TryGetValue("five_lane_drums", out var fivelanes))
                return fivelanes[0].BOOL ? Types.DrumType.FIVE_LANE : Types.DrumType.FOUR_PRO;
            return Types.DrumType.UNKNOWN;
        }
    }
}
