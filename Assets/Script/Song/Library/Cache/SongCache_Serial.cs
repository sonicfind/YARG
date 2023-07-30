using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Serialization;
using YARG.Song.Entries;
using YARG.Util;

#nullable enable
namespace YARG.Song.Library
{
    public class SongCache_Serial : SongCache
    {
        public override void FindNewEntries()
        {
            Progress = ScanProgress.LoadingSongs;
            foreach (string directory in baseDirectories)
            {
                if ((File.GetAttributes(directory) & FileAttributes.Hidden) != FileAttributes.Hidden)
                    ScanDirectory(directory);
            }
            LoadCONSongs();
            LoadExtractedCONSongs();
        }

        public override bool LoadCacheFile()
        {
            Debug.Log($"Attempting to load cache file '{CACHE_FILE}'");
            {
                FileInfo info = new(CACHE_FILE);
                if (!info.Exists || info.Length < 28)
                    return false;
            }

            using FileStream fs = new(CACHE_FILE, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CACHE_VERSION)
                return false;

            using BinaryFileReader reader = new(fs.ReadBytes((int) fs.Length - 4));
            CategoryCacheStrings_Serial strings = new(reader);

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadIniGroup(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpdateDirectory(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpgradeDirectory(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpgradeCON(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadCONGroup(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadExtractedCONGroup(reader, strings);
                reader.ExitSection();
            }
            Debug.Log($"Cache load successful");
            return true;
        }

        public override bool LoadCacheFile_Quick()
        {
            Debug.Log($"Attempting to load cache file '{CACHE_FILE}'");
            {
                FileInfo info = new(CACHE_FILE);
                if (!info.Exists || info.Length < 28)
                    return false;
            }

            using FileStream fs = new(CACHE_FILE, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CACHE_VERSION)
                return false;

            using BinaryFileReader reader = new(fs.ReadBytes((int) fs.Length - 4));
            CategoryCacheStrings_Serial strings = new(reader);

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadIniGroup(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.Position += length;
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadUpgradeDirectory(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadUpgradeCON(reader);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadCONGroup(reader, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadExtractedCONGroup(reader, strings);
                reader.ExitSection();
            }
            Debug.Log($"Cache load successful");
            return true;
        }

        public override void MapCategories()
        {
            Progress = ScanProgress.Sorting;
            foreach (var entryList in entries)
            {
                foreach (var entry in entryList.Value)
                {
                    titles.Add(entry);
                    artists.Add(entry);
                    albums.Add(entry);
                    genres.Add(entry);
                    years.Add(entry);
                    charters.Add(entry);
                    playlists.Add(entry);
                    sources.Add(entry);
                    artistAlbums.Add(entry);
                    songLengths.Add(entry);
                }
            }
        }

        private void ScanDirectory(string directory)
        {
            if (!TraversalPreTest(directory))
                return;

            var charts = new string?[3];
            string? ini = null;
            List<string> subfiles = new();

            try
            {
                foreach (var subFile in Directory.EnumerateFileSystemEntries(directory))
                {
                    string lowercase = Path.GetFileName(subFile).ToLower();
                    if (lowercase == "song.ini")
                    {
                        ini = subFile;
                        continue;
                    }

                    bool found = false;
                    for (int i = 0; i < 3; ++i)
                    {
                        if (lowercase == CHARTTYPES[i].Item1)
                        {
                            charts[i] = subFile;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        subfiles.Add(subFile);
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                Debug.Log(directory);
                return;
            }

            if (ScanIniEntry(charts, ini))
                return;

            foreach (string file in subfiles)
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
                {
                    if ((attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        ScanDirectory(file);
                }
                else
                    AddPossibleCON(file);
            }
        }

        private void LoadCONSongs()
        {
            foreach (var node in conGroups)
                loadCONGroup(node);
        }

        private void LoadExtractedCONSongs()
        {
            foreach (var node in extractedConGroups)
                loadExtractedCONGroup(node);
        }

        private void ReadIniGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory))
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadIniEntry(directory, reader, strings);
                reader.ExitSection();
            }
        }

        private void ReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();
                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                using var entryReader = reader.CreateReaderFromCurrentPosition(length);
                group.ReadEntry(name, index, entryReader, strings);
            }
        }

        private void ReadExtractedCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var group = ReadExtractedCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                int index = reader.ReadInt32();
                int length = reader.ReadInt32();

                if (invalidSongsInCache.Contains(name))
                {
                    reader.Position += length;
                    continue;
                }

                using var entryReader = reader.CreateReaderFromCurrentPosition(length);
                group.ReadEntry(name, index, entryReader, strings);
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory))
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                QuickReadIniEntry(directory, reader, strings);
                reader.ExitSection();
            }
        }

        private void QuickReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                using var entryReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadCONEntry(group!.file, name, entryReader, strings);
            }
        }

        private void QuickReadExtractedCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);
            if (dta == null)
                return;

            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                using var entryReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadExtractedCONEntry(name, dta, entryReader, strings);
            }
        }
    }
}
