using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagLib;
using UnityEngine;
using YARG.Serialization;
using YARG.Util;

#nullable enable
namespace YARG.Song.Library
{
    public class SongCache_Serial : SongCache
    {
        protected override void FindNewEntries(List<string> baseDirectories)
        {
            for (int i = 0; i < baseDirectories.Count; ++i)
                ScanDirectory(new(baseDirectories[i]));
            LoadCONSongs();
            LoadExtractedCONSongs();
        }

        protected override void ScanDirectory(DirectoryInfo directory)
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

        protected override void LoadCONSongs()
        {
            foreach (var node in conGroups)
                loadCONGroup(node);
        }

        protected override void LoadExtractedCONSongs()
        {
            foreach (var node in extractedConGroups)
                loadExtractedCONGroup(node);
        }

        protected override bool LoadCacheFile(List<string> baseDirectories)
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
                ReadIniEntry(reader, baseDirectories, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpdateDirectory(reader, baseDirectories);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpgradeDirectory(reader, baseDirectories);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadUpgradeCON(reader, baseDirectories);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadCONGroup(reader, baseDirectories, strings);
                reader.ExitSection();
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.EnterSection(length);
                ReadExtractedCONGroup(reader, baseDirectories, strings);
                reader.ExitSection();
            }
            Debug.Log($"Cache load successful");
            return true;
        }

        protected override void ReadCONGroup(BinaryFileReader reader, List<string> baseDirectories, CategoryCacheStrings strings)
        {
            string filename = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(filename, baseDirectories))
                return;

            int dtaLastWrite = reader.ReadInt32();
            if (!FindCONGroup(filename, out var group))
            {
                FileInfo info = new(filename);
                if (!info.Exists)
                    return;

                MarkFile(filename);

                var file = CONFile.LoadCON(info.FullName);
                if (file == null)
                    return;

                group = new(file, info.LastWriteTime);
                AddCONGroup(filename, group);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
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

        protected override void ReadExtractedCONGroup(BinaryFileReader reader, List<string> baseDirectories, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory, baseDirectories))
                return;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
                return;

            ExtractedConGroup group = new(dtaInfo);
            MarkDirectory(directory);
            AddExtractedCONGroup(directory, group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
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
                using var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadIniEntry(sectionReader, strings);
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
                using var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadUpgradeDirectory(sectionReader);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                using var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadUpgradeCON(sectionReader);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                using var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadCONGroup(sectionReader, strings);
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                using var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadExtractedCONGroup(sectionReader, strings);
            }
            Debug.Log($"Cache load successful");
            return true;
        }

        protected override void QuickReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 4;
            if (!FindCONGroup(filename, out var group))
            {
                if (!CreateCONGroup(filename, out group))
                    return;
                AddCONGroup(filename, group!);
            }

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

        protected override void QuickReadExtractedCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            reader.Position += reader.ReadLEB();
            reader.Position += 8;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                using var entryReader = reader.CreateReaderFromCurrentPosition(length);
                QuickReadExtractedCONEntry(name, entryReader, strings);
            }
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
    }
}
