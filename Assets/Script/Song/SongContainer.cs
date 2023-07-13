using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Settings;
using YARG.Song.Entries;
using YARG.Song.Library;
using YARG.Util;

namespace YARG.Song
{
    public static class SongContainer
    {
        private static Dictionary<Hash128, List<SongEntry>> _entries;
        private static List<SongEntry>    _songs;
        public static TitleCategory       Titles { get; private set; }
        public static MainCategory        Artists { get; private set; }
        public static MainCategory        Albums { get; private set; }
        public static MainCategory        Genres { get; private set; }
        public static YearCategory        Years { get; private set; }
        public static MainCategory        Charters { get; private set; }
        public static MainCategory        Playlists { get; private set; }
        public static MainCategory        Sources { get; private set; }
        public static ArtistAlbumCategory ArtistAlbums { get; private set; }
        public static SongLengthCategory  SongLengths { get; private set; }

        public static int Count => _songs.Count;

        public static IReadOnlyDictionary<Hash128, List<SongEntry>> SongsByHash => _entries;
        public static IReadOnlyList<SongEntry> Songs => _songs;

        static SongContainer()
        {
            _songs = new();
            Titles = new();
            Artists = new(SongAttribute.ARTIST);
            Albums = new(SongAttribute.ALBUM);
            Genres = new(SongAttribute.GENRE);
            Years = new();
            Charters = new(SongAttribute.CHARTER);
            Playlists = new(SongAttribute.PLAYLIST);
            Sources = new(SongAttribute.SOURCE);
            ArtistAlbums = new();
            SongLengths = new();
        }

        public static async UniTask Scan(bool quick, Action<SongCache> updateUi = null, bool multithreaded = false)
        {
            using SongCache cache = multithreaded ? new SongCache_Parallel() : new SongCache_Serial();
            var scanTask = Task.Run(() =>
            {
                try
                {
                    if (!quick || !cache.QuickScan())
                        cache.FullScan(SettingsManager.Settings.SongFolders, !quick);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            });

            while (!scanTask.IsCompleted)
            {
                updateUi?.Invoke(cache);
                await UniTask.NextFrame();
            }

            if (!quick)
            {
                Debug.Log("Writing badsongs.txt");
                await cache.WriteBadSongs();
                Debug.Log("Finished writing badsongs.txt");
            }
            Set(cache);
        }

        private static void Set(SongCache cache)
        {
            GameManager.Instance.SelectedSong = null;
            
            _entries = cache.entries;
            _songs = new() { Capacity = cache.Count };
            foreach (var node in _entries)
                _songs.AddRange(node.Value);

            Titles = cache.titles;
            Artists = cache.artists;
            Albums = cache.albums;
            Genres = cache.genres;
            Years = cache.years;
            Charters = cache.charters;
            Playlists = cache.playlists;
            Sources = cache.sources;
            ArtistAlbums = cache.artistAlbums;
            SongLengths = cache.songLengths;
        }
    }
}