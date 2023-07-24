﻿using YARG.Serialization;
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
using YARG.Library.CacheNodes;

namespace YARG.Song.Entries
{
    public enum SongAttribute
    {
        UNSPECIFIED,
        TITLE,
        ARTIST,
        ALBUM,
        GENRE,
        YEAR,
        CHARTER,
        PLAYLIST,
        SONG_LENGTH,
        SOURCE,
    };

    
    public abstract class SongEntry
    {
        protected static readonly SortString s_DEFAULT_ARTIST = new("Unknown Artist");
        protected static readonly SortString s_DEFAULT_ALBUM = new("Unknown Album");
        protected static readonly SortString s_DEFAULT_GENRE = new("Unknown Genre");
        protected static readonly SortString s_DEFAULT_YEAR = new("Unknown Year");
        protected static readonly SortString s_DEFAULT_CHARTER = new("Unknown Charter");
        protected static readonly SortString s_DEFAULT_SOURCE = new("Unknown Source");

        protected SortString m_name = string.Empty;
        protected SortString m_artist = s_DEFAULT_ARTIST;
        protected SortString m_album = s_DEFAULT_ALBUM;
        protected SortString m_genre = s_DEFAULT_GENRE;
        protected SortString m_year = s_DEFAULT_YEAR;
        protected SortString m_charter = s_DEFAULT_CHARTER;
        protected SortString m_playlist = string.Empty;
        protected SortString m_source = s_DEFAULT_SOURCE;

        protected ulong m_song_length = 0;
        protected float m_previewStart = 0.0f;
        protected float m_previewEnd = 0.0f;
        protected ushort m_album_track = ushort.MaxValue;
        protected ushort m_playlist_track = ushort.MaxValue;
        protected string m_icon = string.Empty;
        

        protected ulong m_hopo_frequency = 0;

        protected sbyte m_bandIntensity = -1;
        protected TrackScans m_scans = new();

        public SortString Artist => m_artist;
        public SortString Name => m_name;
        public SortString Album => m_album;
        public SortString Genre => m_genre;
        public SortString Year => m_year;
        public SortString Charter => m_charter;
        public SortString Playlist => m_playlist;
        public SortString Source => m_source;
        public ulong SongLength => m_song_length;
        public ulong HopoFrequency => m_hopo_frequency;
        public bool IsMaster { get; protected set; }
        public int VocalParts { get; protected set; }

        public string Directory { get; protected set; } = string.Empty;

        public ScanValues GetValues(NoteTrackType track)
        {
            return track switch
            {
                NoteTrackType.Lead         => m_scans.lead_5,
                NoteTrackType.Lead_6       => m_scans.lead_6,
                NoteTrackType.Bass         => m_scans.lead_5,
                NoteTrackType.Bass_6       => m_scans.lead_6,
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
                _ => throw new ArgumentException("track value is not of a valid type"),
            };
        }

        protected SongEntry() { m_scans = new(1); }

        protected SongEntry(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            m_bandIntensity = reader.ReadSByte();
            m_scans = new(reader);

            m_name.Str = strings.titles[reader.ReadInt32()];
            m_artist.Str = strings.artists[reader.ReadInt32()];
            m_album.Str = strings.albums[reader.ReadInt32()];
            m_genre.Str = strings.genres[reader.ReadInt32()];
            m_year.Str = strings.years[reader.ReadInt32()];
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
            VocalParts       = reader.ReadInt32();
            IsMaster         = reader.ReadBoolean();
        }

        protected void FormatCacheData(BinaryWriter writer, CategoryCacheWriteNode node)
        {
            writer.Write(m_bandIntensity);
            m_scans.WriteToCache(writer);

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
            writer.Write(VocalParts);
            writer.Write(IsMaster);
        }

        public bool IsBelow(SongEntry rhs, SongAttribute attribute)
        {
            if (attribute == SongAttribute.ALBUM)
            {
                if (m_album_track != rhs.m_album_track)
                    return m_album_track < rhs.m_album_track;
            }
            else if (attribute == SongAttribute.PLAYLIST)
            {
                if (m_playlist_track != rhs.m_playlist_track)
                    return m_playlist_track < rhs.m_playlist_track;
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
    }

    public class EntryComparer : IComparer<SongEntry>
    {
        private readonly SongAttribute attribute;

        public EntryComparer(SongAttribute attribute) { this.attribute = attribute; }

        public int Compare(SongEntry x, SongEntry y)  { return x!.IsBelow(y!, attribute) ? -1 : 1; }
    }
}
