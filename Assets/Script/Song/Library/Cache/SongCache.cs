using YARG.Serialization;
using YARG.Song.Entries;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Util;
using YARG.Assets.Script.Types;
using Cysharp.Threading.Tasks;
using YARG.UI;
using YARG.Settings;

#nullable enable
namespace YARG.Song.Library
{
    public enum ScanResult
    {
        Success,
        DirectoryError,
        IniEntryCorruption,
        NoName,
        NoNotes,
        DTAError,
        MissingMogg,
        UnsupportedEncryption,
        MissingMidi,
        PossibleCorruption
    }

    public enum ScanProgress
    {
        LoadingCache,
        LoadingSongs,
        Sorting,
        WritingCache
    }

    public abstract class SongCache : IDisposable
    {
        protected const int CACHE_VERSION = 23_07_25_01;
        protected static readonly object dirLock = new();
        protected static readonly object fileLock = new();
        protected static readonly object iniLock = new();
        protected static readonly object conLock = new();
        protected static readonly object extractedLock = new();
        protected static readonly object updateLock = new();
        protected static readonly object upgradeLock = new();
        protected static readonly object updateGroupLock = new();
        protected static readonly object upgradeGroupLock = new();
        protected static readonly object entryLock = new();
        protected static readonly object badsongsLock = new();
        protected static readonly object invalidLock = new();
        protected static readonly string CACHE_FILE = Path.Combine(PathHelper.PersistentDataPath, "songcache.bin");

        static SongCache() { }

        public readonly Dictionary<Hash128, List<SongEntry>> entries = new();
        public readonly TitleCategory titles = new();
        public readonly MainCategory artists = new(SongAttribute.ARTIST);
        public readonly MainCategory albums = new(SongAttribute.ALBUM);
        public readonly MainCategory genres = new(SongAttribute.GENRE);
        public readonly YearCategory years = new();
        public readonly MainCategory charters = new(SongAttribute.CHARTER);
        public readonly MainCategory playlists = new(SongAttribute.PLAYLIST);
        public readonly MainCategory sources = new(SongAttribute.SOURCE);
        public readonly ArtistAlbumCategory artistAlbums = new();
        public readonly SongLengthCategory songLengths = new();


        public int Count { get { lock (entryLock) return _count; } }
        public int NumScannedDirectories { get { lock (dirLock) return preScannedDirectories.Count; } }
        public int BadSongCount { get { lock (badsongsLock) return badSongs.Count; } }

        public ScanProgress Progress { get; private set; } = ScanProgress.LoadingCache;

        protected readonly string[] baseDirectories;
        protected readonly List<UpdateGroup> updateGroups = new();
        protected readonly Dictionary<string, List<(string, DTAFileReader)>> updates = new();
        protected readonly List<UpgradeGroup> upgradeGroups = new();

        protected readonly Dictionary<string, PackedCONGroup> conGroups = new();
        protected readonly Dictionary<string, (DTAFileReader?, SongProUpgrade)> upgrades = new();
        protected readonly Dictionary<string, ExtractedConGroup> extractedConGroups = new();
        protected readonly Dictionary<Hash128, List<IniSongEntry>> iniEntries = new();
        protected readonly HashSet<string> invalidSongsInCache = new();

        protected readonly HashSet<string> preScannedDirectories = new();
        protected readonly HashSet<string> preScannedFiles = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();

        protected int _count;

        protected readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MIDI),
            new("notes.chart", ChartType.CHART),
        };
        private bool disposedValue;

        protected SongCache() { baseDirectories = SettingsManager.Settings.SongFolders.ToArray(); }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    updateGroups.Clear();
                    upgradeGroups.Capacity = 0;
                    foreach(var update in updates)
                    {
                        update.Value.Clear();
                        update.Value.Capacity = 0;
                    }
                    updates.Clear();
                    upgradeGroups.Clear();
                    upgradeGroups.Capacity = 0;
                    conGroups.Clear();
                    upgrades.Clear();
                    extractedConGroups.Clear();
                    foreach (var entry in iniEntries)
                    {
                        entry.Value.Clear();
                        entry.Value.Capacity = 0;
                    }
                    iniEntries.Clear();
                    preScannedDirectories.Clear();
                    preScannedFiles.Clear();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public bool QuickScan()
        {
            Debug.Log("Performing quick scan");
            Debug.Log($"Attempting to load cache file '{CACHE_FILE}'");
            if (!LoadCacheFile_Quick())
            {
                ToastManager.ToastWarning("Song cache is not present or outdated - rescan required");
                Debug.Log("Cache file unavailable or outdated");
                return false;
            }

            Debug.Log($"Cache load successful");

            Progress = ScanProgress.Sorting;
            MapCategories();
            Debug.Log("Finished quick scan");
            return true;
        }

        public void FullScan(bool loadCache = true)
        {
            Debug.Log("Performing full scan");
            if (loadCache)
            {
                Debug.Log($"Attempting to load cache file '{CACHE_FILE}'");
                if (LoadCacheFile())
                    Debug.Log($"Cache load successful");
                else
                    Debug.Log($"Cache load failed - Unavailable or outdated");
            }

            Debug.Log($"Traversing song directories");
            Progress = ScanProgress.LoadingSongs;
            FindNewEntries();
            FinalizeIniEntries();

            Progress = ScanProgress.Sorting;
            MapCategories();

            Debug.Log($"Attempting to write cache file '{CACHE_FILE}'");
            try
            {
                Progress = ScanProgress.WritingCache;
                SaveToFile();
                Debug.Log($"Cache file write successful");
            }
            catch (Exception ex)
            {
                Debug.Log($"Cache file write unsuccessful");
                Debug.LogError(ex);
            }
            Debug.Log("Full scan complete");
        }

        public async UniTask WriteBadSongs()
        {
#if UNITY_EDITOR
            string badSongsPath = Path.Combine(PathHelper.PersistentDataPath, "badsongs.txt");
#else
            string badSongsPath = Path.Combine(PathHelper.ExecutablePath, "badsongs.txt");
#endif
            await using var stream = new FileStream(badSongsPath, FileMode.Create, FileAccess.Write);
            await using var writer = new StreamWriter(stream);

            foreach (var error in badSongs)
            {
                await writer.WriteLineAsync(error.Key);
                switch (error.Value)
                {
                    case ScanResult.DirectoryError:
                        await writer.WriteLineAsync("Error accessing directory contents");
                        break;
                    case ScanResult.IniEntryCorruption:
                        await writer.WriteLineAsync("Corruption of either the ini file or chart/mid file");
                        break;
                    case ScanResult.NoName:
                        await writer.WriteLineAsync("Name metadata not provided");
                        break;
                    case ScanResult.NoNotes:
                        await writer.WriteLineAsync("No notes found");
                        break;
                    case ScanResult.DTAError:
                        await writer.WriteLineAsync("Error occured while parsing DTA file node");
                        break;
                    case ScanResult.MissingMogg:
                        await writer.WriteLineAsync("Required mogg audio file not present");
                        break;
                    case ScanResult.UnsupportedEncryption:
                        await writer.WriteLineAsync("Mogg file uses unsupported encryption");
                        break;
                    case ScanResult.MissingMidi:
                        await writer.WriteLineAsync("Midi file queried for found missing");
                        break;
                    case ScanResult.PossibleCorruption:
                        await writer.WriteLineAsync("Possible corruption of a queried midi file");
                        break;
                }
                await writer.WriteLineAsync();
            }
        }

        protected abstract void FindNewEntries();
        protected abstract void MapCategories();
        protected abstract bool LoadCacheFile();
        protected abstract bool LoadCacheFile_Quick();

        protected void FinalizeIniEntries()
        {
            foreach (var entryList in iniEntries)
                foreach (var entry in entryList.Value)
                    entry.FinishScan();
        }

        protected bool ScanIniEntry(string?[] charts, string? ini)
        {
            for (int i = ini != null ? 0 : 2; i < 3; ++i)
            {
                var chart = charts[i];
                if (chart != null)
                {
                    try
                    {
                        using FrameworkFile_Alloc file = new(chart);
                        IniSongEntry entry = new(file, chart, ini, CHARTTYPES[i].Item2);
                        if (entry.ScannedSuccessfully())
                        {
                            if (AddEntry(entry))
                                AddIniEntry(entry);
                        }
                        else if (entry.GetModifier("name") == null)
                            AddToBadSongs(chart, ScanResult.NoName);
                        else
                            AddToBadSongs(chart, ScanResult.NoNotes);
                    }
                    catch
                    {
                        AddToBadSongs(Path.GetDirectoryName(chart), ScanResult.IniEntryCorruption);
                    }
                    return true;
                }
            }
            return false;
        }

        protected bool TraversalPreTest(string directory)
        {
            if (!FindOrMarkDirectory(directory))
                return false;

            string filename = Path.GetFileName(directory);
            if (filename == "songs_updates")
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    UpdateGroupAdd(directory, dta);
                    return false;
                }
            }
            else if (filename == "song_upgrades")
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    UpgradeGroupAdd(directory, dta);
                    return false;
                }
            }
            else if (filename == "songs")
            {
                FileInfo dta = new(Path.Combine(directory, "songs.dta"));
                if (dta.Exists)
                {
                    AddExtractedCONGroup(directory, new(dta));
                    return false;
                }
            }
            return true;
        }

        protected void AddPossibleCON(string filename)
        {
            if (!FindOrMarkFile(filename))
                return;

            var file = CONFile.LoadCON(filename);
            if (file == null)
                return;

            PackedCONGroup group = new(file, File.GetLastWriteTime(filename));
            AddCONGroup(filename, group);

            if (group.LoadUpgrades(out var reader))
                AddCONUpgrades(group, reader!);
        }

        protected void loadCONGroup(KeyValuePair<string, PackedCONGroup> node)
        {
            var group = node.Value;
            if (group.LoadSongs(out var reader))
            {
                Dictionary<string, int> indices = new();
                while (reader!.StartNode())
                {
                    string name = reader.GetNameOfNode();
                    int index;
                    if (indices.ContainsKey(name))
                        index = ++indices[name];
                    else
                        index = indices[name] = 0;

                    if (group.TryGetEntry(name, index, out var entry))
                    {
                        if (!AddEntry(entry!))
                            group.RemoveEntry(name, index);
                    }
                    else
                    {
                        try
                        {
                            ConSongEntry currentSong = new(group.file, name, reader);
                            var result = ProcessCONEntry(name, currentSong);
                            if (result == ScanResult.Success)
                            {
                                if (AddEntry(currentSong))
                                    group.AddEntry(name, index, currentSong);
                            }
                            else
                            {
                                AddToBadSongs(node.Key + $" - Node {name}", result);
                            }
                        }
                        catch (Exception e)
                        {
                            AddToBadSongs(node.Key + $" - Node {name}", ScanResult.DTAError);
                            Debug.LogError(e);
                        }
                    }
                    reader.EndNode();
                }
            }
        }

        protected void loadExtractedCONGroup(KeyValuePair<string, ExtractedConGroup> node)
        {
            string directory = node.Key;
            var group = node.Value;
            var reader = group.reader;

            Dictionary<string, int> indices = new();
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                int index;
                if (indices.ContainsKey(name))
                    index = indices[name]++;
                else
                    index = indices[name] = 0;

                if (group.TryGetEntry(name, index, out var entry))
                {
                    if (!AddEntry(entry!))
                        group.RemoveEntry(name, index);
                }
                else
                {
                    try
                    {
                        ConSongEntry currentSong = new(directory, group.dta, name, reader);
                        var result = ProcessCONEntry(name, currentSong);
                        if (result == ScanResult.Success)
                        {
                            if (AddEntry(currentSong))
                                group.AddEntry(name, index, currentSong);
                        }
                        else
                        {
                            AddToBadSongs(node.Key + $" - Node {name}", result);
                        }
                    }
                    catch (Exception e)
                    {
                        AddToBadSongs(node.Key + $" - Node {name}", ScanResult.DTAError);
                        Debug.LogError(e.Message);
                    }
                }
                reader.EndNode();
            }
        }

        protected bool FindOrMarkDirectory(string directory)
        {
            lock (dirLock)
            {
                if (preScannedDirectories.Contains(directory))
                    return false;

                preScannedDirectories.Add(directory);
                return true;
            }
        }

        protected bool StartsWithBaseDirectory(string path)
        {
            for (int i = 0; i != baseDirectories.Length; ++i)
                if (path.StartsWith(baseDirectories[i]))
                    return true;
            return false;
        }

        protected void ReadIniEntry(string baseDirectory, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHARTTYPES.Length)
                return;

            ref var chartType = ref CHARTTYPES[chartTypeIndex];
            FileInfo chartFile = new(Path.Combine(directory, chartType.Item1));
            if (!chartFile.Exists)
                return;

            if (chartFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                iniFile = new(Path.Combine(directory, "song.ini"));
                if (!iniFile.Exists)
                    return;

                if (iniFile.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            MarkDirectory(directory);
            IniSongEntry entry = new(directory, chartFile, iniFile, chartType.Item2, reader, strings);
            AddEntry(entry);
            AddIniEntry(entry);
        }

        protected void ReadUpdateDirectory(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            if (StartsWithBaseDirectory(directory))
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    UpdateGroupAdd(directory, dta);

                    if (dta.LastWriteTime == dtaLastWrite)
                        return;
                }
            }

            for (int i = 0; i < count; i++)
                AddInvalidSong(reader.ReadLEBString());
        }

        protected void ReadUpgradeDirectory(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            if (StartsWithBaseDirectory(directory))
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    var group = UpgradeGroupAdd(directory, dta);

                    if (group != null && dta.LastWriteTime == dtaLastWrite)
                    {
                        for (int i = 0; i < count; i++)
                        {
                            string name = reader.ReadLEBString();
                            var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                            if (!group.upgrades.TryGetValue(name, out var upgrade) || upgrade!.Midi.LastWriteTime != lastWrite)
                                AddInvalidSong(name);
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(reader.ReadLEBString());
                reader.Position += 8;
            }
        }

        protected void ReadUpgradeCON(BinaryFileReader cacheReader)
        {
            string filename = cacheReader.ReadLEBString();
            var conLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            var dtaLastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
            int count = cacheReader.ReadInt32();

            if (StartsWithBaseDirectory(filename) && CreateCONGroup(filename, out var group))
            {
                AddCONGroup(filename, group!);
                if (group!.LoadUpgrades(out var reader))
                {
                    AddCONUpgrades(group, reader!);

                    if (group.UpgradeDTALastWrite == dtaLastWrite)
                    {
                        if (group.lastWrite != conLastWrite)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                string name = cacheReader.ReadLEBString();
                                var lastWrite = DateTime.FromBinary(cacheReader.ReadInt64());
                                if (group.upgrades[name].Midi.LastWriteTime != lastWrite)
                                    AddInvalidSong(name);
                            }
                        }
                        return;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(cacheReader.ReadLEBString());
                cacheReader.Position += 8;
            }
        }

        protected PackedCONGroup? ReadCONGroupHeader(BinaryFileReader reader)
        {
            string filename = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(filename))
                return null;

            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                FileInfo info = new(filename);
                if (!info.Exists)
                    return null;

                MarkFile(filename);

                var file = CONFile.LoadCON(info.FullName);
                if (file == null)
                    return null;

                group = new(file, info.LastWriteTime);
                AddCONGroup(filename, group);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
                return null;
            return group;
        }

        protected ExtractedConGroup? ReadExtractedCONGroupHeader(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory))
                return null;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
                return null;

            ExtractedConGroup group = new(dtaInfo);
            MarkDirectory(directory);
            AddExtractedCONGroup(directory, group);

            if (dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return group;
        }

        protected void QuickReadIniEntry(string baseDirectory, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CHARTTYPES.Length)
                return;

            ref var chartType = ref CHARTTYPES[chartTypeIndex];
            var lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo chartFile = new(Path.Combine(directory, chartType.Item1), lastWrite);
            AbridgedFileInfo? iniFile = null;
            if (reader.ReadBoolean())
            {
                lastWrite = DateTime.FromBinary(reader.ReadInt64());
                iniFile = new(Path.Combine(directory, "song.ini"), lastWrite);
            }

            IniSongEntry entry = new(directory, chartFile, iniFile, chartType.Item2, reader, strings);
            AddEntry(entry);
        }

        protected void QuickReadUpgradeDirectory(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            int count = reader.ReadInt32();

            UpgradeGroup group = new(directory, dtaLastWrite);
            lock (upgradeGroupLock)
                upgradeGroups.Add(group);

            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadLEBString();
                var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                SongProUpgrade upgrade = new(filename, lastWrite);
                group.upgrades.Add(name, upgrade);
                AddUpgrade(name, null, upgrade);
            }
        }

        protected void QuickReadUpgradeCON(BinaryFileReader reader)
        {
            string filename = reader.ReadLEBString();
            reader.Position += 12;
            int count = reader.ReadInt32();

            if (CreateCONGroup(filename, out var group))
            {
                var file = group!.file;
                AddCONGroup(filename, group);

                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());
                    var listing = file[$"songs_upgrades/{name}_plus.mid"];

                    SongProUpgrade upgrade = new(file, listing, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadLEBString();
                    var lastWrite = DateTime.FromBinary(reader.ReadInt64());

                    SongProUpgrade upgrade = new(null, null, lastWrite);
                    AddUpgrade(name, null, upgrade);
                }
            }
        }

        protected PackedCONGroup? QuickReadCONGroupHeader(BinaryFileReader reader)
        {
            string filename = reader.ReadLEBString();
            var dtaLastWrite = DateTime.FromBinary(reader.ReadInt64());
            if (!FindCONGroup(filename, out var group))
            {
                if (!CreateCONGroup(filename, out group))
                    return null;
                AddCONGroup(filename, group!);
            }

            if (!group!.SetSongDTA() || group.DTALastWrite != dtaLastWrite)
                return null;
            return group;
        }

        protected AbridgedFileInfo? QuickReadExtractedCONGroupHeader(BinaryFileReader reader)
        {
            string directory = reader.ReadLEBString();
            if (!StartsWithBaseDirectory(directory))
                return null;

            FileInfo dtaInfo = new(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists || dtaInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return null;
            return new(dtaInfo);
        }

        protected void QuickReadCONEntry(CONFile file, string nodeName, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var midiListing = file[reader.ReadLEBString()];
            var midiLastWrite = DateTime.FromBinary(reader.ReadInt64());

            FileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file[reader.ReadLEBString()];
                reader.Position += 8;
            }
            else
            {
                string moggName = reader.ReadLEBString();
                var moggTime = DateTime.FromBinary(reader.ReadInt64());
                moggInfo = new(moggName, moggTime);
            }

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                string updateName = reader.ReadLEBString();
                DateTime updateTime = DateTime.FromBinary(reader.ReadInt64());
                updateInfo = new(updateName, updateTime);
            }

            ConSongEntry currentSong = new(file, nodeName, midiListing, midiLastWrite, moggListing, moggInfo, updateInfo, reader, strings);
            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            AddEntry(currentSong);
        }

        protected void QuickReadExtractedCONEntry(string nodeName, AbridgedFileInfo dta, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string filename = reader.ReadLEBString();
            var lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo midiInfo = new(filename, lastWrite);

            bool isYargMogg = reader.ReadBoolean();
            filename = reader.ReadLEBString();
            lastWrite = DateTime.FromBinary(reader.ReadInt64());

            AbridgedFileInfo moggInfo = new(filename, lastWrite);

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                filename = reader.ReadLEBString();
                lastWrite = DateTime.FromBinary(reader.ReadInt64());
                updateInfo = new(filename, lastWrite);
            }

            ConSongEntry currentSong;
            if (isYargMogg)
                currentSong = new(midiInfo, dta, moggInfo, null, updateInfo, reader, strings);
            else
                currentSong = new(midiInfo, dta, null, moggInfo, updateInfo, reader, strings);

            if (upgrades.TryGetValue(nodeName, out var upgrade))
                currentSong.Upgrade = upgrade.Item2;

            AddEntry(currentSong);
        }

        protected bool CreateCONGroup(string filename, out PackedCONGroup? group)
        {
            group = null;

            FileInfo info = new(filename);
            if (!info.Exists)
                return false;

            MarkFile(filename);

            var file = CONFile.LoadCON(filename);
            if (file == null)
                return false;

            group = new(file, info.LastWriteTime);
            return true;
        }

        protected void AddCONGroup(string filename, PackedCONGroup group)
        {
            lock (conLock) 
                conGroups.Add(filename, group);
        }

        protected void AddExtractedCONGroup(string directory, ExtractedConGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(directory, group);
        }

        protected bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conLock)
                return conGroups.TryGetValue(filename, out group);
        }

        protected void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        protected void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
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

        private void AddInvalidSong(string name)
        {
            lock (invalidLock) invalidSongsInCache.Add(name);
        }

        private void AddIniEntry(IniSongEntry entry)
        {
            var hash = entry.Hash;
            lock (iniLock)
            {
                if (iniEntries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    iniEntries.Add(hash, new() { entry });
            }
        }

        private void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badsongsLock) badSongs.Add(filePath, err);
        }

        private void UpdateGroupAdd(string directory, FileInfo dta, bool removeEntries = false)
        {
            DTAFileReader reader = new(dta.FullName);
            UpdateGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                group!.updates.Add(name);

                (string, DTAFileReader) node = new(directory, reader.Clone());
                lock (updateLock)
                {
                    if (updates.TryGetValue(name, out var list))
                        list.Add(node);
                    else
                        updates[name] = new() { node };
                }

                if (removeEntries)
                    RemoveCONEntry(name);
                reader.EndNode();
            }

            if (group!.updates.Count > 0)
                lock (updateGroupLock)
                    updateGroups.Add(group);
        }

        private UpgradeGroup? UpgradeGroupAdd(string directory, FileInfo dta, bool removeEntries = false)
        {
            DTAFileReader reader = new(dta.FullName);
            UpgradeGroup group = new(directory, dta.LastWriteTime);
            while (reader!.StartNode())
            {
                string name = reader.GetNameOfNode();
                FileInfo file = new(Path.Combine(directory, $"{name}_plus.mid"));
                if (file.Exists)
                {
                    if (CanAddUpgrade(name, file.LastWriteTime))
                    {
                        SongProUpgrade upgrade = new(file.FullName, file.LastWriteTime);
                        group!.upgrades[name] = upgrade;
                        AddUpgrade(name, reader.Clone(), upgrade);

                        if (removeEntries)
                            RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }

            if (group.upgrades.Count > 0)
            {
                lock (upgradeGroupLock)
                    upgradeGroups.Add(group);
                return group;
            }
            return null;
        }

        private void AddUpgrade(string name, DTAFileReader? reader, SongProUpgrade upgrade)
        {
            lock (upgradeLock)
                upgrades[name] = new(reader, upgrade);
        }

        private void AddCONUpgrades(PackedCONGroup group, DTAFileReader reader)
        {
            var file = group.file;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                var listing = file[$"songs_upgrades/{name}_plus.mid"];

                if (listing != null)
                {
                    if (CanAddUpgrade_CONInclusive(name, listing.lastWrite))
                    {
                        SongProUpgrade upgrade = new(file, listing, listing.lastWrite);
                        group.upgrades[name] = upgrade;
                        AddUpgrade(name, reader.Clone(), upgrade);
                        RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }
        }

        private ScanResult ProcessCONEntry(string name, ConSongEntry currentSong)
        {
            if (updates.TryGetValue(name, out var updateList))
            {
                foreach (var update in updateList!)
                    currentSong.Update(update.Item1, name, update.Item2.Clone());
            }

            if (upgrades.TryGetValue(name, out var upgrade))
            {
                currentSong.Upgrade = upgrade.Item2;
                currentSong.SetFromDTA(name, upgrade.Item1!.Clone());
            }

            return currentSong.Scan(name);
        }

        private bool CanAddUpgrade(string shortname, DateTime lastWrite)
        {
            lock (upgradeLock)
            {
                foreach (var group in upgradeGroups)
                {
                    if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.Midi.LastWriteTime >= lastWrite)
                            return false;
                        group.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }

        private bool CanAddUpgrade_CONInclusive(string shortname, DateTime lastWrite)
        {
            lock (conLock)
            {
                foreach (var group in conGroups)
                {
                    var upgrades = group.Value.upgrades;
                    if (upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade!.Midi.LastWriteTime >= lastWrite)
                            return false;
                        upgrades.Remove(shortname);
                        return true;
                    }
                }
            }

            return CanAddUpgrade(shortname, lastWrite);
        }

        private void RemoveCONEntry(string shortname)
        {
            lock (conLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in conGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    conGroups.Remove(entriesToRemove[i]);
            }

            lock (extractedLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in extractedConGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    extractedConGroups.Remove(entriesToRemove[i]);
            }
        }

        private bool AddEntry(SongEntry entry)
        {
            var hash = entry.Hash;
            lock (entryLock)
            {
                if (entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    entries.Add(hash, new() { entry });
                ++_count;
            }
            return true;
        }

        private void SaveToFile()
        {
            using var writer = new BinaryWriter(new FileStream(CACHE_FILE, FileMode.Create, FileAccess.Write));
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            writer.Write(CACHE_VERSION);

            titles.WriteToCache(writer, ref nodes);
            artists.WriteToCache(writer, ref nodes);
            albums.WriteToCache(writer, ref nodes);
            genres.WriteToCache(writer, ref nodes);
            years.WriteToCache(writer, ref nodes);
            charters.WriteToCache(writer, ref nodes);
            playlists.WriteToCache(writer, ref nodes);
            sources.WriteToCache(writer, ref nodes);

            writer.Write(baseDirectories.Length);
            foreach (string baseDirectory in baseDirectories)
            {
                byte[] buffer = FormatIniEntriesToCache(baseDirectory, nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(updateGroups.Count);
            foreach (var group in updateGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(upgradeGroups.Count);
            foreach (var group in upgradeGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            List<KeyValuePair<string, PackedCONGroup>> upgradeCons = new();
            List<KeyValuePair<string, PackedCONGroup>> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Value.UpgradeCount > 0)
                    upgradeCons.Add(group);

                if (group.Value.EntryCount > 0)
                    entryCons.Add(group);
            }

            writer.Write(upgradeCons.Count);
            foreach (var group in upgradeCons)
            {
                byte[] buffer = group.Value.FormatUpgradesForCache(group.Key);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(entryCons.Count);
            foreach (var group in entryCons)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key, ref nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(extractedConGroups.Count);
            foreach (var group in extractedConGroups)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key, ref nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }

        private byte[] FormatIniEntriesToCache(string baseDirectory, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<(string, IniSongEntry)> group = new();
            foreach (var node in iniEntries)
            {
                for (int i = 0; i < node.Value.Count;)
                {
                    var entry = node.Value[i];
                    string directory = entry.Directory;
                    string relative = Path.GetRelativePath(baseDirectory, directory);  
                    if (relative.Length < directory.Length)
                    {
                        group.Add(new(relative, entry));
                        node.Value.RemoveAt(i);
                    }
                    else
                        ++i;
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(baseDirectory);
            writer.Write(group.Count);
            foreach ((string relative, var entry) in group)
            {
                byte[] buffer = entry.FormatCacheData(relative, nodes[entry]);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            return ms.ToArray();
        }
    }
}
