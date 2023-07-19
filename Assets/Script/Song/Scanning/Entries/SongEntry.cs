using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Ini;
using YARG.Modifiers;
using System.Runtime.InteropServices;
using YARG.Song.Entries.TrackScan;
using System.IO;
using System.Xml.Linq;
using System.Diagnostics;
using YARG.Audio;
using YARG.Song.Library;
using YARG.Data;
using UnityEngine;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;

namespace YARG.Song.Entries
{
    public enum SongAttribute
    {
        UNSPECIFIED,
        TITLE,
        ARTIST,
        ALBUM,
        ARTIST_ALBUM,
        GENRE,
        YEAR,
        CHARTER,
        PLAYLIST,
        SOURCE,
        SONG_LENGTH,
    };

    
    public abstract class SongEntry
    {
        protected static readonly SortString s_DEFAULT_ARTIST = new("Unknown Artist");
        protected static readonly SortString s_DEFAULT_ALBUM = new("Unknown Album");
        protected static readonly SortString s_DEFAULT_GENRE = new("Unknown Genre");
        protected const string DEFAULT_YEAR = "Unknown Year";
        protected static readonly SortString s_DEFAULT_CHARTER = new("Unknown Charter");
        protected static readonly SortString s_DEFAULT_SOURCE = new("Unknown Source");

        protected SortString m_name = string.Empty;
        protected SortString m_artist = s_DEFAULT_ARTIST;
        protected SortString m_album = s_DEFAULT_ALBUM;
        protected SortString m_genre = s_DEFAULT_GENRE;
        protected SortString m_charter = s_DEFAULT_CHARTER;
        protected SortString m_playlist = string.Empty;
        protected SortString m_source = s_DEFAULT_SOURCE;

        private string m_unmodifiedYear = DEFAULT_YEAR;
        private string m_parsedYear = DEFAULT_YEAR;
        private int m_yearAsInt = int.MaxValue;
        private static readonly Regex s_YearRegex = new(@"(\d{4})");

        protected ulong m_song_length = 0;
        protected float m_previewStart = 0.0f;
        protected float m_previewEnd = 0.0f;
        protected ushort m_album_track = ushort.MaxValue;
        protected ushort m_playlist_track = ushort.MaxValue;
        protected string m_icon = string.Empty;
        protected float m_delay = 0;
        protected string m_loadingPhrase = string.Empty;

        protected ulong m_hopo_frequency = 170;

        protected TrackScans m_scans = new();
        protected Hash128 m_hash = default;

        public Hash128 Hash => m_hash;

        public SortString Artist => m_artist;
        public SortString Name => m_name;
        public SortString Album => m_album;
        public SortString Genre => m_genre;
        public string Year
        {
            get { return m_parsedYear; }
            protected set
            {
                m_unmodifiedYear = value;
                var match = s_YearRegex.Match(value);
                if (string.IsNullOrEmpty(match.Value))
                    m_parsedYear = value;
                else
                {
                    m_parsedYear = match.Value[..4];
                    m_yearAsInt = int.Parse(m_parsedYear);
                }
            }
        }

        public string UnmodifiedYear => m_unmodifiedYear;

        public int YearAsNumber
        {
            get { return m_yearAsInt; }
            protected set
            {
                m_yearAsInt = value;
                m_parsedYear = m_unmodifiedYear = value.ToString();
            }
        }

        public SortString Charter => m_charter;
        public SortString Playlist => m_playlist;
        public SortString Source => m_source;
        public ulong SongLength => m_song_length;
        public ulong HopoFrequency => m_hopo_frequency;
        public bool IsMaster { get; protected set; }
        public int VocalParts { get; protected set; }
        public float Delay => m_delay;
        public string LoadingPhrase => m_loadingPhrase;

        public TimeSpan SongLengthTimeSpan => TimeSpan.FromMilliseconds(m_song_length);
        public TimeSpan PreviewStartTimeSpan => TimeSpan.FromMilliseconds(m_previewStart);
        public TimeSpan PreviewEndTimeSpan => TimeSpan.FromMilliseconds(m_previewEnd);

        protected sbyte m_bandIntensity = -1;
        public sbyte BandDifficulty => m_bandIntensity;

        public string Directory { get; protected set; } = string.Empty;

        public string GetStringAttribute(SongAttribute attribute)
        {
            return attribute switch
            {
                SongAttribute.TITLE => m_name.Str,
                SongAttribute.ARTIST => m_artist.Str,
                SongAttribute.ALBUM => m_album.Str,
                SongAttribute.GENRE => m_genre.Str,
                SongAttribute.YEAR => m_unmodifiedYear,
                SongAttribute.CHARTER => m_charter.Str,
                SongAttribute.PLAYLIST => m_playlist.Str,
                SongAttribute.SOURCE => m_source.Str,
                _ => throw new Exception("stoopid"),
            };
        }

        public ScanValues GetValues(NoteTrackType track)
        {
            return track switch
            {
                NoteTrackType.Lead         => m_scans.lead_5,
                NoteTrackType.Lead_6       => m_scans.lead_6,
                NoteTrackType.Bass         => m_scans.bass_5,
                NoteTrackType.Bass_6       => m_scans.bass_6,
                NoteTrackType.Rhythm       => m_scans.rhythm,
                NoteTrackType.Coop         => m_scans.coop,
                NoteTrackType.Keys         => m_scans.keys,
                NoteTrackType.Drums_4      => m_scans.drums_4,
                NoteTrackType.Drums_4Pro   => m_scans.drums_4pro,
                NoteTrackType.Drums_5      => m_scans.drums_5,
                NoteTrackType.Vocals       => m_scans.leadVocals,
                NoteTrackType.Harmonies    => m_scans.harmonyVocals,
                NoteTrackType.ProGuitar_17 => m_scans.proguitar_17,
                NoteTrackType.ProGuitar_22 => m_scans.proguitar_22,
                NoteTrackType.ProBass_17   => m_scans.probass_17,
                NoteTrackType.ProBass_22   => m_scans.probass_22,
                NoteTrackType.ProKeys      => m_scans.proKeys,
                _ => throw new ArgumentException("track value is not of a valid type"),
            };
        }

        public bool HasPart(Instrument inst, int diff)
        {
            return inst switch
            {
                Instrument.GUITAR =>      m_scans.lead_5[diff],
                Instrument.BASS =>        m_scans.bass_5[diff],
                Instrument.DRUMS =>       m_scans.drums_4[diff],
                Instrument.KEYS =>        m_scans.keys[diff],
                Instrument.VOCALS =>      m_scans.leadVocals[diff],
                Instrument.REAL_GUITAR => m_scans.proguitar_17[diff] || m_scans.proguitar_22[diff],
                Instrument.REAL_BASS =>   m_scans.probass_17[diff] || m_scans.probass_22[diff],
                Instrument.REAL_DRUMS =>  m_scans.drums_4pro[diff],
                Instrument.REAL_KEYS =>   m_scans.proKeys[diff],
                Instrument.HARMONY =>     m_scans.harmonyVocals[diff],
                Instrument.GH_DRUMS =>    m_scans.drums_5[diff],
                Instrument.RHYTHM =>      m_scans.rhythm[diff],
                Instrument.GUITAR_COOP => m_scans.coop[diff],
                _ => false,
            };
        }

        public bool HasInstrument(Instrument inst)
        {
            return inst switch
            {
                Instrument.GUITAR => m_scans.lead_5.subTracks > 0,
                Instrument.BASS => m_scans.bass_5.subTracks > 0,
                Instrument.DRUMS => m_scans.drums_4.subTracks > 0,
                Instrument.KEYS => m_scans.keys.subTracks > 0,
                Instrument.VOCALS => m_scans.leadVocals.subTracks > 0,
                Instrument.REAL_GUITAR => m_scans.proguitar_17.subTracks > 0 || m_scans.proguitar_22.subTracks > 0,
                Instrument.REAL_BASS => m_scans.probass_17.subTracks > 0 || m_scans.probass_22.subTracks > 0,
                Instrument.REAL_DRUMS => m_scans.drums_4pro.subTracks > 0,
                Instrument.REAL_KEYS => m_scans.proKeys.subTracks > 0,
                Instrument.HARMONY => m_scans.harmonyVocals.subTracks > 0,
                Instrument.GH_DRUMS => m_scans.drums_5.subTracks > 0,
                Instrument.RHYTHM => m_scans.rhythm.subTracks > 0,
                Instrument.GUITAR_COOP => m_scans.coop.subTracks > 0,
                _ => false,
            };
        }

        protected SongEntry() { m_scans = new(); }

        protected SongEntry(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            m_bandIntensity = reader.ReadSByte();
            m_scans = new(reader);
            SetVocalsCount();

            {
                ulong ul_1 = reader.ReadUInt64();
                ulong ul_2 = reader.ReadUInt64();
                m_hash = new(ul_1, ul_2);
            }

            m_name.Str = strings.titles[reader.ReadInt32()];
            m_artist.Str = strings.artists[reader.ReadInt32()];
            m_album.Str = strings.albums[reader.ReadInt32()];
            m_genre.Str = strings.genres[reader.ReadInt32()];
            Year = strings.years[reader.ReadInt32()];
            m_charter.Str = strings.charters[reader.ReadInt32()];
            m_playlist.Str = strings.playlists[reader.ReadInt32()];
            m_source.Str = strings.sources[reader.ReadInt32()];

            m_previewStart   = reader.ReadFloat();
            m_previewEnd     = reader.ReadFloat();
            m_album_track    = reader.ReadUInt16();
            m_playlist_track = reader.ReadUInt16();
            m_song_length    = reader.ReadUInt64();
            m_icon           = reader.ReadLEBString();
            m_hopo_frequency = reader.ReadUInt64();
            IsMaster         = reader.ReadBoolean();
            m_delay          = reader.ReadInt32();
            m_loadingPhrase  = reader.ReadLEBString();
        }

        protected void FormatCacheData(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(m_bandIntensity);
            m_scans.WriteToCache(writer);
            unsafe
            {
                var hash = m_hash;
                writer.Write(new ReadOnlySpan<byte>((byte*)&hash, sizeof(Hash128)));
            }

            writer.Write(node.title);
            writer.Write(node.artist);
            writer.Write(node.album);
            writer.Write(node.genre);
            writer.Write(node.year);
            writer.Write(node.charter);
            writer.Write(node.playlist);
            writer.Write(node.source);

            writer.Write(m_previewStart);
            writer.Write(m_previewEnd);
            writer.Write(m_album_track);
            writer.Write(m_playlist_track);
            writer.Write(m_song_length);
            writer.Write(m_icon);
            writer.Write(m_hopo_frequency);
            writer.Write(IsMaster);
            writer.Write(m_delay);
            writer.Write(m_loadingPhrase);
        }

        public bool IsBelow(SongEntry rhs, SongAttribute attribute)
        {
            switch (attribute)
            {
                case SongAttribute.ALBUM:
                    if (m_album_track != rhs.m_album_track)
                        return m_album_track < rhs.m_album_track;
                    break;
                case SongAttribute.YEAR:
                    if (m_yearAsInt != rhs.m_yearAsInt)
                        return m_yearAsInt < rhs.m_yearAsInt;
                    break;
                case SongAttribute.PLAYLIST:
                    if (m_playlist_track != rhs.m_playlist_track)
                        return m_playlist_track < rhs.m_playlist_track;

                    if (m_bandIntensity != rhs.m_bandIntensity)
                        return m_bandIntensity < rhs.m_bandIntensity;
                    break;
                case SongAttribute.SONG_LENGTH:
                    if (m_song_length != rhs.m_song_length)
                        return m_song_length < rhs.m_song_length;
                    break;
            }

            int strCmp;
            if ((strCmp = m_name.CompareTo(rhs.m_name)) != 0 ||
                (strCmp = m_artist.CompareTo(rhs.m_artist)) != 0 ||
                (strCmp = m_album.CompareTo(rhs.m_album)) != 0 ||
                (strCmp = m_charter.CompareTo(rhs.m_charter)) != 0)
                return strCmp < 0;
            else
                return Directory.CompareTo(rhs.Directory) < 0;
        }

        public DrumType GetDrumType()
        {
            if (m_scans.drums_4.subTracks > 0)
                return DrumType.FOUR_PRO;

            if (m_scans.drums_5.subTracks > 0)
                return DrumType.FIVE_LANE;

            return DrumType.UNKNOWN;
        }

        protected bool Scan_Midi(FrameworkFile file, DrumType drumType, bool cymbals)
        {
            using MidiFileReader reader = new(file);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events && type != MidiTrackType.Beat)
                        m_scans.ScanFromMidi(type, drumType, ref cymbals, reader);
                }
            }
            return cymbals;
        }

        protected void SetVocalsCount()
        {
            if (m_scans.harmonyVocals.subTracks > 0)
            {
                int count = 0;
                for (int i = 1; i < 8; i <<= 1)
                    if ((m_scans.harmonyVocals.subTracks & i) > 0)
                        ++count;
                VocalParts = count;
            }
            else if (m_scans.leadVocals.subTracks > 0)
                VocalParts = 1;
            else
                VocalParts = 0;
        }

        public abstract void LoadAudio(IAudioManager manager, float speed, params SongStem[] songStems);
        public abstract UniTask<bool> LoadPreviewAudio(IAudioManager manager, float speed);
        //public abstract YARGSong? LoadChart();
        public abstract YargChart LoadChart_Original();
    }

    public class EntryComparer : IComparer<SongEntry>
    {
        private readonly SongAttribute attribute;

        public EntryComparer(SongAttribute attribute) { this.attribute = attribute; }

        public SortString GetKey(SongEntry entry)
        {
            return attribute switch
            {
                SongAttribute.ARTIST => entry.Artist,
                SongAttribute.ALBUM => entry.Album,
                SongAttribute.GENRE => entry.Genre,
                SongAttribute.CHARTER => entry.Charter,
                SongAttribute.PLAYLIST => entry.Playlist,
                SongAttribute.SOURCE => entry.Source,
                _ => throw new Exception("stoopid"),
            };
        }

        public int Compare(SongEntry x, SongEntry y)  { return x!.IsBelow(y!, attribute) ? -1 : 1; }
    }

    public class InstrumentComparer : IComparer<SongEntry>
    {
        private readonly NoteTrackType instrument;
        public InstrumentComparer(NoteTrackType instrument)
        {
            this.instrument = instrument;
        }

        public int Compare(SongEntry x, SongEntry y)
        {
            sbyte intensity_x = x.GetValues(instrument).intensity;
            sbyte intensity_y = y.GetValues(instrument).intensity;

            if (intensity_x < intensity_y)
                return -1;

            if (intensity_y < intensity_x)
                return 1;

            return x!.IsBelow(y!, SongAttribute.UNSPECIFIED) ? -1 : 1;
        }
    }
}
