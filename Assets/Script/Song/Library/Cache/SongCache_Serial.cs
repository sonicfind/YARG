﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using UnityEngine;
using YARG.Serialization;
using YARG.Song.Entries;
using YARG.Util;

#nullable enable
namespace YARG.Song.Library
{
    public class SongCache_Serial : SongCache
    {
        protected override void FindNewEntries()
        {
            for (int i = 0; i < baseDirectories.Length; ++i)
                ScanDirectory(new(baseDirectories[i]));
            LoadCONSongs();
            LoadExtractedCONSongs();
        }

        protected override bool LoadCacheFile()
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

        protected override bool LoadCacheFile_Quick()
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

        protected override void MapCategories()
        {
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

        private void ScanDirectory(DirectoryInfo directory)
        {
            string dirName = directory.FullName;
            if ((directory.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden || !FindOrMarkDirectory(dirName))
                return;

            if (TryAddUpdateDirectory(directory) || TryAddUpgradeDirectory(directory) || TryAddExtractedCONDirectory(directory))
                return;

            var charts = new FileInfo?[3];
            FileInfo? ini = null;
            List<DirectoryInfo> subDirectories = new();
            List<FileInfo> files = new();

            try
            {
                foreach (var info in directory.EnumerateFileSystemInfos())
                {
                    string filename = info.Name.ToLower();
                    if ((info.Attributes & FileAttributes.Directory) > 0)
                    {
                        subDirectories.Add((info as DirectoryInfo)!);
                        continue;
                    }

                    var file = (info as FileInfo)!;
                    if (filename == "song.ini")
                    {
                        ini = file;
                        continue;
                    }

                    bool found = false;
                    for (int i = 0; i < 3; ++i)
                    {
                        if (filename == CHARTTYPES[i].Item1)
                        {
                            charts[i] = file;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                        files.Add(file);
                }
            }
            catch (Exception e)
            {
                Debug.Log(e.Message);
                Debug.Log(directory.FullName);
                return;
            }

            if (ScanIniEntry(charts, ini))
                return;

            for (int i = 0; i < files.Count; ++i)
                AddPossibleCON(files[i]);

            for (int i = 0; i < subDirectories.Count; ++i)
                ScanDirectory(subDirectories[i]);
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