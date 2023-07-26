using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Ini;
using YARG.Modifiers;
using YARG.Serialization;
using YARG.Song.Chart.DrumTrack;
using YARG.Song.Chart.Notes;
using YARG.Song.Entries;
using YARG.Types;

namespace YARG.Song.Chart
{
    public class YARGIniSong : YARGSong
    {
        private const string DEFAULT_NAME = "Unknown Title";
        private const string DEFAULT_ARTIST = "Unknown Artist";
        private const string DEFAULT_ALBUM = "Unknown Album";
        private const string DEFAULT_GENRE = "Unknown Genre";
        private const string DEFAULT_YEAR = "Unknown Year";
        private const string DEFAULT_CHARTER = "Unknown Charter";

        private static readonly Dictionary<string, ModifierNode> MODIFIER_LIST = new()
        {
            { "Album",         new("album", ModifierNodeType.STRING_CHART ) },
            { "Artist",        new("artist", ModifierNodeType.STRING_CHART ) },
            { "BassStream",    new("BassStream", ModifierNodeType.STRING_CHART ) },
            { "Charter",       new("charter", ModifierNodeType.STRING_CHART ) },
            { "CrowdStream",   new("CrowdStream", ModifierNodeType.STRING_CHART ) },
            { "Difficulty",    new("diff_band", ModifierNodeType.INT32 ) },
            { "Drum2Stream",   new("Drum2Stream", ModifierNodeType.STRING_CHART ) },
            { "Drum3Stream",   new("Drum3Stream", ModifierNodeType.STRING_CHART ) },
            { "Drum4Stream",   new("Drum4Stream", ModifierNodeType.STRING_CHART ) },
            { "DrumStream",    new("DrumStream", ModifierNodeType.STRING_CHART ) },
            { "Genre",         new("genre", ModifierNodeType.STRING_CHART ) },
            { "GuitarStream",  new("GuitarStream", ModifierNodeType.STRING_CHART ) },
            { "HarmonyStream", new("HarmonyStream", ModifierNodeType.STRING_CHART ) },
            { "KeysStream",    new("KeysStream", ModifierNodeType.STRING_CHART ) },
            { "MusicStream",   new("MusicStream", ModifierNodeType.STRING_CHART ) },
            { "Name",          new("name", ModifierNodeType.STRING_CHART ) },
            { "Offset",        new("delay", ModifierNodeType.FLOAT ) },
            { "PreviewEnd",    new("preview_end_time", ModifierNodeType.FLOAT ) },
            { "PreviewStart",  new("preview_start_time", ModifierNodeType.FLOAT ) },
            { "Resolution",    new("Resolution", ModifierNodeType.UINT16 ) },
            { "RhythmStream",  new("RhythmStream", ModifierNodeType.STRING_CHART ) },
            { "VocalStream",   new("VocalStream", ModifierNodeType.STRING_CHART ) },
            { "Year",          new("year", ModifierNodeType.STRING_CHART ) },
        };

        private static readonly Dictionary<string, ModifierNode> TICKRATE_LIST = new()
        {
            { "Resolution", new("Resolution", ModifierNodeType.UINT16 ) },
        };

        internal static readonly byte[] SECTION = Encoding.ASCII.GetBytes("section ");
        internal static readonly byte[] LYRIC = Encoding.ASCII.GetBytes("lyric ");
        internal static readonly byte[] PHRASE_START = Encoding.ASCII.GetBytes("phrase_start");
        internal static readonly byte[] PHRASE_END = Encoding.ASCII.GetBytes("phrase_end ");

        static YARGIniSong() { }

        private ulong m_sustain_cutoff_threshold = 0;
        private ushort m_hopofreq_old = ushort.MaxValue;
        private bool m_eighthnote_hopo = false;
        private byte m_multiplier_note = 116;
        private Dictionary<string, Modifier> m_modifiers = new();

        public YARGIniSong() { }
        public YARGIniSong(string directory) : base(directory) { }
        public YARGIniSong(IniSongEntry entry) : base(entry)
        {
            m_sustain_cutoff_threshold = entry.SustainCutoffThreshold;
            m_hopofreq_old = entry.Hopofreq_Old;
            m_eighthnote_hopo = entry.EightNoteHopo;
            m_multiplier_note = entry.MultiplierNote;
            m_sync.Tickrate = 192;
        }

        public void Load_Midi(string filepath)
        {
            using MidiFileReader reader = new(filepath, m_multiplier_note);
            m_sync.Tickrate = reader.GetTickRate();
            SetSustainThreshold();
            Parse(reader, Encoding.UTF8);
            FinalizeData();
        }

        public void Load_Chart(string path, bool fullLoad)
        {
            using ChartFileReader reader = new(path);
            if (!reader.ValidateHeaderTrack())
                throw new Exception("[Song] track expected at the start of the file");

            ParseHeaderTrack(fullLoad ? MODIFIER_LIST : TICKRATE_LIST, reader);
            SetSustainThreshold();

            LegacyDrumTrack legacy = new(m_baseDrumType);
            while (reader.IsStartOfTrack())
            {
                if (reader.ValidateSyncTrack())
                    m_sync.AddFromDotChart(reader);
                else if (reader.ValidateEventsTrack())
                {
                    ulong phrase = ulong.MaxValue;
                    while (reader.IsStillCurrentTrack())
                    {
                        var trackEvent = reader.ParseEvent();
                        if (trackEvent.Item2 == ChartEvent.EVENT)
                        {
                            var str = reader.ExtractText();
                            if (str.StartsWith(SECTION)) m_events.sections.Get_Or_Add_Back(trackEvent.Item1) = Encoding.UTF8.GetString(str[8..]);
                            else if (str.StartsWith(LYRIC)) m_tracks.leadVocals[0][trackEvent.Item1].lyric = Encoding.UTF8.GetString(str[6..]);
                            else if (str.SequenceEqual(PHRASE_START))
                            {
                                if (phrase < ulong.MaxValue)
                                    m_tracks.leadVocals.specialPhrases[phrase].Add(new(SpecialPhraseType.LyricLine, trackEvent.Item1 - phrase));
                                phrase = trackEvent.Item1;
                            }
                            else if (str.SequenceEqual(PHRASE_END))
                            {
                                if (phrase < ulong.MaxValue)
                                {
                                    m_tracks.leadVocals.specialPhrases[phrase].Add(new(SpecialPhraseType.LyricLine, trackEvent.Item1 - phrase));
                                    phrase = ulong.MaxValue;
                                }
                            }
                            else
                                m_events.globals.Get_Or_Add_Back(trackEvent.Item1).Add(Encoding.UTF8.GetString(str));
                        }
                        reader.NextEvent();
                    }
                }
                else if (!reader.ValidateDifficulty() || !reader.ValidateInstrument() || !m_tracks.LoadFromDotChart(ref legacy, reader))
                    reader.SkipTrack();
            }

            if (legacy.IsOccupied())
            {
                if (legacy.Type == DrumType.FIVE_LANE)
                    legacy.Transfer(m_tracks.drums5);
                else
                    legacy.Transfer(m_tracks.drums_4pro);
            }

            FinalizeData();
        }

        public void Load_Ini()
        {
            string iniFile = Path.Combine(m_directory, "song.ini");
            if (!File.Exists(iniFile))
                return;

            var modifiers = IniHandler.ReadSongIniFile(iniFile);
            if (modifiers.Remove("name", out var names))
                for (int i = 0; i < names.Count; ++i)
                {
                    m_name = names[i].SORTSTR.Str;
                    if (m_name != "Unknown Title")
                        break;
                }

            if (modifiers.Remove("artist", out var artists))
                for (int i = 0; i < artists.Count; ++i)
                {
                    m_artist = artists[i].SORTSTR.Str;
                    if (m_artist != "Unknown Artist")
                        break;
                }

            if (modifiers.Remove("album", out var albums))
                for (int i = 0; i < albums.Count; ++i)
                {
                    m_album = albums[i].SORTSTR.Str;
                    if (m_album != "Unknown Album")
                        break;
                }

            if (modifiers.Remove("genre", out var genres))
                for (int i = 0; i < genres.Count; ++i)
                {
                    m_genre = genres[i].SORTSTR.Str;
                    if (m_genre != "Unknown Genre")
                        break;
                }

            if (modifiers.Remove("year", out var years))
                for (int i = 0; i < years.Count; ++i)
                {
                    m_year = years[i].SORTSTR.Str;
                    if (m_year != "Unknown Year")
                        break;
                }

            if (modifiers.Remove("charter", out var charters))
                for (int i = 0; i < charters.Count; ++i)
                {
                    m_charter = charters[i].SORTSTR.Str;
                    if (m_charter != "Unknown Charter")
                        break;
                }

            if (modifiers.Remove("playlist", out var playlists))
            {
                string parentPlaylist = Path.GetDirectoryName(m_directory)!;
                for (int i = 0; i < playlists.Count; ++i)
                {
                    m_playlist = playlists[i].SORTSTR.Str;
                    if (m_playlist != parentPlaylist)
                        break;
                }
            }

            if (modifiers.Remove("five_lane_drums", out var fivelanes))
                m_baseDrumType = fivelanes[0].BOOL ? DrumType.FIVE_LANE : DrumType.FOUR_PRO;

            if (modifiers.Remove("hopo_frequency", out var hopo_freq))
                m_hopo_frequency = hopo_freq[0].UINT64;

            if (modifiers.Remove("multiplier_note", out var multiplier))
                if (multiplier[0].UINT16 == 103)
                    m_multiplier_note = 103;

            if (modifiers.Remove("eighthnote_hopo", out var eighthnote))
                m_eighthnote_hopo = eighthnote[0].BOOL;

            if (modifiers.Remove("sustain_cutoff_threshold", out var threshold))
                m_sustain_cutoff_threshold = threshold[0].UINT64;

            if (modifiers.Remove("hopofreq", out var hopofreq_old))
                m_hopofreq_old = hopofreq_old[0].UINT16;

            foreach (var mod in modifiers)
                m_modifiers.Add(mod.Key, mod.Value[0]);
        }

        private void ParseHeaderTrack(Dictionary<string, ModifierNode> list, ChartFileReader reader)
        {
            var modifiers = reader.ExtractModifiers(list);
            if (modifiers.Remove("Resolution", out var tickrate))
            {
                m_sync.Tickrate = tickrate[0].UINT16;
                if (modifiers.Count == 0)
                    return;
            }

            if (modifiers.Remove("Name", out var names))
            {
                int i = 0;
                if (m_name.Length == 0 || m_name == DEFAULT_NAME)
                {
                    m_name = names[0].STR;
                    ++i;
                }

                while (m_name != DEFAULT_NAME && i < names.Count)
                    m_name = names[i++].SORTSTR.Str;
            }

            if (modifiers.Remove("Artist", out var artists))
            {
                int i = 0;
                if (m_artist.Length == 0 || m_artist == DEFAULT_ARTIST)
                {
                    m_artist = artists[0].STR;
                    ++i;
                }

                while (m_artist != DEFAULT_ARTIST && i < artists.Count)
                    m_artist = artists[i++].SORTSTR.Str;
            }

            if (modifiers.Remove("Album", out var albums))
            {
                int i = 0;
                if (m_album.Length == 0 || m_album == DEFAULT_ALBUM)
                {
                    m_album = albums[0].STR;
                    ++i;
                }

                while (m_album != DEFAULT_ALBUM && i < albums.Count)
                    m_album = albums[i++].SORTSTR.Str;
            }

            if (modifiers.Remove("Genre", out var genres))
            {
                int i = 0;
                if (m_genre.Length == 0 || m_genre == DEFAULT_GENRE)
                {
                    m_genre = genres[0].STR;
                    ++i;
                }

                while (m_genre != DEFAULT_GENRE && i < genres.Count)
                    m_genre = genres[i++].SORTSTR.Str;
            }

            if (modifiers.Remove("Year", out var years))
            {
                int i = 0;
                if (m_year.Length == 0 || m_year == DEFAULT_YEAR)
                {
                    m_year = years[0].STR;
                    ++i;
                }

                while (m_year != DEFAULT_YEAR && i < years.Count)
                    m_year = years[i++].SORTSTR.Str;
            }

            if (modifiers.Remove("Charter", out var charters))
            {
                int i = 0;
                if (m_charter.Length == 0 || m_charter == DEFAULT_CHARTER)
                {
                    m_charter = charters[0].STR;
                    ++i;
                }

                while (m_charter != DEFAULT_CHARTER && i < charters.Count)
                    m_charter = charters[i++].SORTSTR.Str;
            }

            foreach (var modifier in modifiers)
                if (!m_modifiers.ContainsKey(modifier.Key))
                    m_modifiers.Add(modifier.Key, modifier.Value[0]);
        }

        private void SetSustainThreshold()
        {
	        TruncatableSustain.MinDuration = m_sustain_cutoff_threshold > 0 ? m_sustain_cutoff_threshold : (m_sync.Tickrate / 3);
        }

        private ulong GetHopoThreshold()
        {
	        if (m_hopo_frequency > 0)
		        return m_hopo_frequency;
	        else if (m_eighthnote_hopo)
		        return (m_sync.Tickrate / 2);
	        else if (m_hopofreq_old != ushort.MaxValue)
	        {
		        switch (m_hopofreq_old)
		        {
		        case 0:
			        return (m_sync.Tickrate / 24);
		        case 1:
			        return (m_sync.Tickrate / 16);
		        case 2:
			        return (m_sync.Tickrate / 12);
		        case 3:
			        return (m_sync.Tickrate / 8);
		        case 4:
			        return (m_sync.Tickrate / 6);
		        default:
			        return (m_sync.Tickrate / 4);
		        }
	        }
	        else
		        return (m_sync.Tickrate / 3);
        }
    }
}
