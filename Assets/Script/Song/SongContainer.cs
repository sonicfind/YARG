using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Settings;
using YARG.Song.Entries;
using YARG.Song.Library;
using YARG.Types;
using YARG.UI;
using YARG.Util;

namespace YARG.Song
{
    public class SongContainer
    {
        private Dictionary<Hash128, List<SongEntry>> _entries;
        private List<SongEntry> _songs;

        public FlatMap<string, List<SongEntry>> Titles { get; private set; }
        public FlatMap<string, List<SongEntry>> Years { get; private set; }
        public FlatMap<string, List<SongEntry>> ArtistAlbums { get; private set; }
        public FlatMap<string, List<SongEntry>> SongLengths { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Artists { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Albums { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Genres { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Charters { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Playlists { get; private set; }
        public FlatMap<SortString, List<SongEntry>> Sources { get; private set; }

        public int Count => _songs.Count;
        public IReadOnlyDictionary<Hash128, List<SongEntry>> SongsByHash => _entries;
        public IReadOnlyList<SongEntry> Songs => _songs;

        public SongContainer()
        {
            _entries = new();
            _songs = new();
            Titles = new();
            Years = new();
            ArtistAlbums = new();
            SongLengths = new();
            Artists = new();
            Albums = new();
            Genres = new();
            Charters = new();
            Playlists = new();
            Sources = new();
        }

        public SongContainer(SongCache cache)
        {
            GameManager.Instance.SelectedSong = null;

            _entries = cache.entries;
            _songs = new();
            foreach (var node in _entries)
                _songs.AddRange(node.Value);
            _songs.TrimExcess();

            Titles = cache.titles.GetSongSelectionList();
            Years = cache.years.GetSongSelectionList();
            ArtistAlbums = cache.artistAlbums.GetSongSelectionList();
            SongLengths = cache.songLengths.GetSongSelectionList();
            Artists = cache.artists.GetSongSelectionList();
            Albums = cache.albums.GetSongSelectionList();
            Genres = cache.genres.GetSongSelectionList();
            Charters = cache.charters.GetSongSelectionList();
            Playlists = cache.playlists.GetSongSelectionList();
            Sources = cache.sources.GetSongSelectionList();
        }
    }
}