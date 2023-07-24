﻿using YARG.Hashes;
using YARG.Serialization;
using YARG.Song.Entries;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

#nullable enable
namespace YARG.Song.Library
{
    public partial class SongCache
    {
        public static SongLibrary ScanDirectories(List<string> baseDirectories, string cacheFileDirectory, bool writeCache, bool multithreaded = true)
        {
            string cacheFile = Path.Combine(cacheFileDirectory, "songcache_CS.bin");

            using SongCache cache = new();
            if (multithreaded)
                cache.LoadCacheFile(cacheFile, baseDirectories);
            else
                cache.LoadCacheFile_Serial(cacheFile, baseDirectories);

            Parallel.For(0, baseDirectories.Count, i => cache!.ScanDirectory(new(baseDirectories[i])));
            Task.WaitAll(Task.Run(cache.LoadCONSongs), Task.Run(cache.LoadExtractedCONSongs));
            cache.FinalizeIniEntries();

            if (multithreaded)
                cache.MapCategories();
            else
                cache.MapCategories_Serial();

            if (writeCache)
                cache.SaveToFile(cacheFile);

            return cache.library;
        }

        private void ScanDirectory(DirectoryInfo directory)
        {
            string dirName = directory.FullName;
            if (!FindOrMarkDirectory(dirName))
                return;

            if (TryAddUpdateDirectory(directory) || TryAddUpgradeDirectory(directory))
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
                Console.WriteLine(e.Message);
                Console.WriteLine(directory.FullName);
                return;
            }

            if (ScanIniEntry(charts, ini))
                return;

            if (AddExtractedCONDirectory(dirName))
                return;

            Parallel.For(0, files.Count, i => AddPossibleCON(files[i]));
            Parallel.For(0, subDirectories.Count, i => ScanDirectory(subDirectories[i]));
        }

        private bool ScanIniEntry(FileInfo?[] charts, FileInfo? ini)
        {
            for (int i = ini != null ? 0 : 2; i < 3; ++i)
            {
                var chart = charts[i];
                if (chart != null)
                {
                    try
                    {
                        using FrameworkFile_Alloc file = new(chart.FullName);
                        IniSongEntry entry = new(file, chart, ini, ref CHARTTYPES[i]);
                        if (entry.ScannedSuccessfully())
                        {
                            Hash128Wrapper hash = new(file.CalcHash128());
                            if (AddEntry(hash, entry))
                                AddIniEntry(hash, entry);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                        Console.WriteLine(chart.FullName);
                    }
                    return true;
                }
            }
            return false;
        }

        private bool TryAddUpdateDirectory(DirectoryInfo directory)
        {
            if (directory.Name == "songs_updates")
            {
                string dirName = directory.FullName;
                FileInfo dta = new(Path.Combine(dirName, "songs_updates.dta"));
                if (dta.Exists)
                {
                    UpdateGroupAdd(dirName, dta);
                    return true;
                }
            }
            return false;
        }

        private bool TryAddUpgradeDirectory(DirectoryInfo directory)
        {
            if (directory.Name == "song_upgrades")
            {
                string dirName = directory.FullName;
                FileInfo dta = new(Path.Combine(dirName, "upgrades.dta"));
                if (dta.Exists)
                {
                    UpgradeGroupAdd(dirName, dta);
                    return true;
                }
            }
            return false;
        }

        private bool AddExtractedCONDirectory(string directory)
        {
            string songPath = Path.Combine(directory, "songs");
            FileInfo dta = new(Path.Combine(songPath, "songs.dta"));
            if (!dta.Exists)
                return false;

            MarkDirectory(songPath);
            AddExtractedCONGroup(directory, new(dta.FullName, dta.LastWriteTime));
            return true;
        }

        private void AddPossibleCON(FileInfo info)
        {
            if (!FindOrMarkFile(info.FullName))
                return;

            var file = CONFile.LoadCON(info.FullName);
            if (file == null)
                return;

            PackedCONGroup group = new(file, info.LastWriteTime);
            AddCONGroup(info.FullName, group);

            if (group.LoadUpgrades(out var reader))
                AddCONUpgrades(group, reader!);
        }

        private void LoadCONSongs()
        {
            Parallel.ForEach(conGroups, node => {
                var group = node.Value;
                if (group.LoadSongs(out var reader))
                {
                    Dictionary<string, int> indices = new();
                    ushort nodeIndex = 0;
                    while (reader!.StartNode())
                    {
                        string name = reader.GetNameOfNode();
                        int index;
                        if (indices.ContainsKey(name))
                            index = ++indices[name];
                        else
                            index = indices[name] = 0;

                        if (group.TryGetEntry(name, index, out var entryNode))
                        {
                            if (!AddEntry(entryNode!.hash, entryNode.entry))
                                group.RemoveEntry(name, index);
                        }
                        else
                        {
                            try
                            {
                                ConSongEntry currentSong = new(group.file, name, reader, nodeIndex);
                                if (ProcessCONEntry(name, currentSong, out var hash))
                                {
                                    if (AddEntry(hash!, currentSong))
                                        group.AddEntry(name, index, currentSong, hash!);
                                }
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine($"CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                                Debug.WriteLine(e.Message);
                                break;
                            }
                        }
                        reader.EndNode();
                        ++nodeIndex;
                    }
                }
            });
        }

        private void LoadExtractedCONSongs()
        {
            Parallel.ForEach(extractedConGroups, node =>
            {
                string directory = node.Key;
                var group = node.Value;
                var reader = group.LoadDTA();
                if (reader == null)
                    return;

                Dictionary<string, int> indices = new();
                ushort nodeIndex = 0;
                while (reader.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index;
                    if (indices.ContainsKey(name))
                        index = indices[name]++;
                    else
                        index = indices[name] = 0;

                    if (group.TryGetEntry(name, index, out var entryNode))
                    {
                        if (!AddEntry(entryNode!.hash, entryNode.entry))
                            group.RemoveEntry(name, index);
                    }
                    else
                    {
                        try
                        {
                            ConSongEntry currentSong = new(directory, name, reader, nodeIndex);
                            if (ProcessCONEntry(name, currentSong, out var hash))
                            {
                                if (AddEntry(hash!, currentSong))
                                    group.AddEntry(name, index, currentSong, hash!);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.WriteLine($"CON: DTA Failed to parse song '{name}'. Skipping all further songs in file...");
                            Debug.WriteLine(e.Message);
                            break;
                        }
                    }
                    reader.EndNode();
                    ++nodeIndex;
                }
            });
        }

        private bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (preScannedDirectories.Contains(directory))
                    return false;

                preScannedDirectories.Add(directory);
                return true;
            }
        }

        private bool FindOrMarkFile(string file)
        {
            lock (fileLock)
            {
                if (preScannedFiles.Contains(file))
                    return false;

                preScannedFiles.Add(file);
                return true;
            }
        }
    }
}
