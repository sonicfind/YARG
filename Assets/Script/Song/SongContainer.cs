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
    public static class SongContainer
    {
        private static Dictionary<Hash128, List<SongEntry>> _entries;
        private static List<SongEntry>    _songs;

        private static FlatMap<SortString, List<SongEntry>> artists_sort;
        private static FlatMap<SortString, List<SongEntry>> albums_sort;
        private static FlatMap<SortString, List<SongEntry>> genres_sort;
        private static FlatMap<SortString, List<SongEntry>> charters_sort;
        private static FlatMap<SortString, List<SongEntry>> playlists_sort;
        private static FlatMap<SortString, List<SongEntry>> sources_sort;

        public static FlatMap<string, List<SongEntry>> Titles { get; private set; }
        public static FlatMap<string, List<SongEntry>> Artists { get; private set; }
        public static FlatMap<string, List<SongEntry>> Albums { get; private set; }
        public static FlatMap<string, List<SongEntry>> Genres { get; private set; }
        public static FlatMap<string, List<SongEntry>> Years { get; private set; }
        public static FlatMap<string, List<SongEntry>> Charters { get; private set; }
        public static FlatMap<string, List<SongEntry>> Playlists { get; private set; }
        public static FlatMap<string, List<SongEntry>> Sources { get; private set; }
        public static FlatMap<string, List<SongEntry>> ArtistAlbums { get; private set; }
        public static FlatMap<string, List<SongEntry>> SongLengths { get; private set; }

        public static int Count => _songs.Count;
        public static IReadOnlyDictionary<Hash128, List<SongEntry>> SongsByHash => _entries;
        public static IReadOnlyList<SongEntry> Songs => _songs;

        static SongContainer()
        {
            _songs = new();
            Titles = new();
            Artists = new();
            Albums = new();
            Genres = new();
            Years = new();
            Charters = new();
            Playlists = new();
            Sources = new();
            ArtistAlbums = new();
            SongLengths = new();
        }

        public static async UniTask Scan(bool quick, bool multithreaded, Action<SongCache> updateUi = null)
        {
            using SongCache cache = multithreaded ? new SongCache_Parallel() : new SongCache_Serial();
            var scanTask = Task.Run(() =>
            {
                try
                {
                    if (!quick || !QuickScan(cache))
                        FullScan(cache, !quick);
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

        private static bool QuickScan(SongCache cache)
        {
            Debug.Log("Performing quick scan");
            if (!cache.LoadCacheFile_Quick())
            {
                ToastManager.ToastWarning("Song cache is not present or outdated - rescan required");
                Debug.Log("Cache file unavailable or outdated");
                return false;
            }
            Debug.Log($"Cache load successful");
            cache.MapCategories();
            Debug.Log("Finished quick scan");
            return true;
        }

        private static void FullScan(SongCache cache, bool loadCache = true)
        {
            Debug.Log("Performing full scan");
            if (loadCache)
            {
                if (cache.LoadCacheFile())
                    Debug.Log($"Cache load successful");
                else
                    Debug.Log($"Cache load failed - Unavailable or outdated");
            }

            Debug.Log($"Traversing song directories");
            cache.FindNewEntries();
            cache.FinalizeIniEntries();
            cache.MapCategories();

            try
            {
                cache.SaveToFile();
                Debug.Log($"Cache file write successful");
            }
            catch (Exception ex)
            {
                Debug.Log($"Cache file write unsuccessful");
                Debug.LogError(ex);
            }
            Debug.Log("Full scan complete");
        }

        private static void Set(SongCache cache)
        {
            GameManager.Instance.SelectedSong = null;
            
            _entries = cache.entries;
            _songs = new() { Capacity = cache.Count };
            foreach (var node in _entries)
                _songs.AddRange(node.Value);

            Titles = cache.titles.GetSongSelectionList();
            Years = cache.years.GetSongSelectionList();
            ArtistAlbums = cache.artistAlbums.GetSongSelectionList();
            SongLengths = cache.songLengths.GetSongSelectionList();

            artists_sort = cache.artists.GetSongSelectionList();
            albums_sort = cache.albums.GetSongSelectionList();
            genres_sort = cache.genres.GetSongSelectionList();
            charters_sort = cache.charters.GetSongSelectionList();
            playlists_sort = cache.playlists.GetSongSelectionList();
            sources_sort = cache.sources.GetSongSelectionList();

            Artists = Convert(artists_sort);
            Albums = Convert(albums_sort);
            Genres = Convert(genres_sort);
            Charters = Convert(charters_sort);
            Playlists = Convert(playlists_sort);
            Sources = Convert(sources_sort);

            static FlatMap<string, List<SongEntry>> Convert(FlatMap<SortString, List<SongEntry>> list)
            {
                FlatMap<string, List<SongEntry>> map = new();
                foreach (FlatMapNode<SortString, List<SongEntry>> node in list)
                    map.Add(node.key, node.obj);
                return map;
            }
        }

        public static FlatMap<string, List<SongEntry>> GetSongList(SongAttribute sort)
        {
            return sort switch
            {
                SongAttribute.TITLE => Titles,
                SongAttribute.ARTIST => Artists,
                SongAttribute.ALBUM => Albums,
                SongAttribute.GENRE => Genres,
                SongAttribute.YEAR => Years,
                SongAttribute.CHARTER => Charters,
                SongAttribute.PLAYLIST => Playlists,
                SongAttribute.SOURCE => Sources,
                SongAttribute.ARTIST_ALBUM => ArtistAlbums,
                SongAttribute.SONG_LENGTH => SongLengths,
                _ => throw new Exception("stoopid"),
            };
        }

        public static FlatMap<string, List<SongEntry>> Search(SongAttribute sort, string arg)
        {
            if (sort == SongAttribute.TITLE)
            {
                if (arg.Length == 0)
                {
                    FlatMap<string, List<SongEntry>> titleMap = new();
                    foreach (FlatMapNode<string, List<SongEntry>> element in Titles)
                        titleMap.Add(element.key, new(element.obj));
                    return titleMap;
                }

                int i = 0;
                while (i + 1 < arg.Length && !char.IsLetterOrDigit(arg[i]))
                    ++i;

                char character = arg[i];
                string key = char.IsDigit(character) ? "0-9" : char.ToUpper(character).ToString();
                var search = Titles[key];

                List<SongEntry> result = new(search.Count);
                foreach (var element in search)
                    if (element.Name.SortStr.Contains(arg))
                        result.Add(element);
                return new() { { key, result } };
            }

            FlatMap<string, List<SongEntry>> map = new();
            if (sort == SongAttribute.YEAR)
            {
                List<SongEntry> entries = new();
                foreach (FlatMapNode<string, List<SongEntry>> element in Years)
                    foreach (var entry in element.obj)
                        if (entry.Year.Contains(arg))
                            entries.Add(entry);
                return new() { { arg, entries } };
            }

            var elements = sort switch
            {
                SongAttribute.ARTIST => artists_sort,
                SongAttribute.ALBUM => albums_sort,
                SongAttribute.SOURCE => sources_sort,
                SongAttribute.GENRE => genres_sort,
                SongAttribute.CHARTER => charters_sort,
                SongAttribute.PLAYLIST => playlists_sort,
                _ => throw new Exception("stoopid"),
            };

            foreach (FlatMapNode<SortString, List<SongEntry>> element in elements)
            {
                if (arg.Length == 0)
                {
                    map.Add(element.key, new(element.obj));
                    continue;
                }

                string key = element.key.SortStr;
                if (sort == SongAttribute.ARTIST)
                    key = SongSorting.RemoveArticle(key);

                if (key.Contains(arg))
                    map.Add(element.key, new(element.obj));
            }
            return map;
        }
    }
}