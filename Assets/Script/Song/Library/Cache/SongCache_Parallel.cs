using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Serialization;
using YARG.Util;

#nullable enable
namespace YARG.Song.Library
{
    public class CacheHandler_Parallel : CacheHandler
    {
        public CacheHandler_Parallel(SongCache cache) : base(cache) { }

        protected override void FindNewEntries()
        {
            Progress = ScanProgress.LoadingSongs;
            Parallel.ForEach(CacheConstants.baseDirectories, directory => { ScanDirectory(directory); });
            Task.WaitAll(Task.Run(LoadCONSongs), Task.Run(LoadExtractedCONSongs));
        }

        protected override bool LoadCacheFile()
        {
            Progress = ScanProgress.LoadingCache;
            {
                FileInfo info = new(CacheConstants.FILE);
                if (!info.Exists || info.Length < 28)
                    return false;
            }

            using FileStream fs = new(CacheConstants.FILE, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CacheConstants.VERSION)
                return false;

            using BinaryFileReader reader = new(fs.ReadBytes((int) fs.Length - 4));
            CategoryCacheStrings_Parallel strings = new(reader);

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadIniGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    ReadUpdateDirectory(sectionReader);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    ReadUpgradeDirectory(sectionReader);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    ReadUpgradeCON(sectionReader);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadCONGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadExtractedCONGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
            return true;
        }

        protected override bool LoadCacheFile_Quick()
        {
            Progress = ScanProgress.LoadingCache;
            {
                FileInfo info = new(CacheConstants.FILE);
                if (!info.Exists || info.Length < 28)
                    return false;
            }

            using FileStream fs = new(CacheConstants.FILE, FileMode.Open, FileAccess.Read);
            if (fs.ReadInt32LE() != CacheConstants.VERSION)
                return false;

            using BinaryFileReader reader = new(fs.ReadBytes((int) fs.Length - 4));
            CategoryCacheStrings_Parallel strings = new(reader);

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadIniGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                reader.Position += length;
            }

            List<Task> conTasks = new();
            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    QuickReadUpgradeDirectory(sectionReader);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                conTasks.Add(Task.Run(() =>
                {
                    QuickReadUpgradeCON(sectionReader);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(conTasks.ToArray());

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadCONGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var sectionReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadExtractedCONGroup(sectionReader, strings);
                    sectionReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
            return true;
        }

        protected override void SortCategories()
        {
            Progress = ScanProgress.Sorting;
            Parallel.ForEach(cache.entries, entryList =>
            {
                foreach (var entry in entryList.Value)
                {
                    cache.titles.Add(entry);
                    cache.artists.Add(entry);
                    cache.albums.Add(entry);
                    cache.genres.Add(entry);
                    cache.years.Add(entry);
                    cache.charters.Add(entry);
                    cache.playlists.Add(entry);
                    cache.sources.Add(entry);
                    cache.artistAlbums.Add(entry);
                    cache.songLengths.Add(entry);
                }
            });
        }

        private void ScanDirectory(string directory)
        {
            if (!TraversalPreTest(directory))
                return;

            try
            {
                DirectorySearcher result = new(directory);
                if (ScanIniEntry(result))
                    return;

                Parallel.ForEach(result.subfiles, file =>
                {
                    var attributes = File.GetAttributes(file);
                    if ((attributes & FileAttributes.Directory) != 0)
                        ScanDirectory(file);
                    else
                        AddPossibleCON(file);
                });
            }
            catch (Exception e)
            {
                AddErrors(directory + ": " + e.Message);
            }
        }

        private void LoadCONSongs()
        {
            Parallel.ForEach(cache.conGroups, node => loadCONGroup(node));
        }

        private void LoadExtractedCONSongs()
        {
            Parallel.ForEach(cache.extractedConGroups, loadExtractedCONGroup);
        }

        private void ReadIniGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (!CacheConstants.StartsWithBaseDirectory(directory))
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    ReadIniEntry(directory, entryReader, strings);
                    entryReader.Dispose();
                }));
            }
            Task.WaitAll(entryTasks.ToArray());
        }

        private void ReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var group = ReadCONGroupHeader(reader);
            if (group == null)
                return;

            List<Task> entryTasks = new();
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

                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    group.ReadEntry(name, index, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
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

                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    group.ReadEntry(name, index, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadIniGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = reader.ReadLEBString();
            if (!CacheConstants.StartsWithBaseDirectory(directory))
                return;

            List<Task> entryTasks = new();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; ++i)
            {
                int length = reader.ReadInt32();
                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadIniEntry(directory, entryReader, strings);
                    entryReader.Dispose();
                }));
            }
            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var group = QuickReadCONGroupHeader(reader);
            if (group == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadCONEntry(group!.file, name, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }

        private void QuickReadExtractedCONGroup(BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var dta = QuickReadExtractedCONGroupHeader(reader);
            if (dta == null)
                return;

            int count = reader.ReadInt32();
            List<Task> entryTasks = new();
            for (int i = 0; i < count; ++i)
            {
                string name = reader.ReadLEBString();
                reader.Position += 4;
                int length = reader.ReadInt32();

                var entryReader = reader.CreateReaderFromCurrentPosition(length);
                entryTasks.Add(Task.Run(() =>
                {
                    QuickReadExtractedCONEntry(name, dta, entryReader, strings);
                    entryReader.Dispose();
                }));
            }

            Task.WaitAll(entryTasks.ToArray());
        }
    }
}
