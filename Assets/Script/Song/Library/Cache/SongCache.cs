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
using Cysharp.Threading.Tasks;
using YARG.UI;
using YARG.Settings;
using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine.PlayerLoop;
using MoonscraperChartEditor.Song;

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
        WritingCache,
        WritingBadSongs
    }

    public static class CacheConstants
    {
        public static readonly string FILE = Path.Combine(PathHelper.PersistentDataPath, "songcache.bin");
        public const int VERSION = 23_07_28_05;
        public static readonly(string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MIDI),
            new("notes.chart", ChartType.CHART),
        };

        public static string[] baseDirectories = Array.Empty<string>();

        public static bool StartsWithBaseDirectory(string path)
        {
            for (int i = 0; i != baseDirectories.Length; ++i)
                if (path.StartsWith(baseDirectories[i]))
                    return true;
            return false;
        }
    }

    public class SongCache
    {
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

        public readonly List<UpdateGroup> updateGroups = new();
        public readonly List<UpgradeGroup> upgradeGroups = new();
        public readonly Dictionary<string, PackedCONGroup> conGroups = new();
        public readonly Dictionary<string, ExtractedConGroup> extractedConGroups = new();
        public readonly Dictionary<Hash128, List<IniSongEntry>> iniEntries = new();

        public List<SongEntry> ToEntryList()
        {
            List<SongEntry> songs = new();
            foreach (var node in entries)
                songs.AddRange(node.Value);
            songs.TrimExcess();
            return songs;
        }
    }

    public abstract class CacheHandler
    {
        private static readonly object dirLock = new();
        private static readonly object fileLock = new();
        private static readonly object iniLock = new();
        private static readonly object conLock = new();
        private static readonly object extractedLock = new();
        private static readonly object updateLock = new();
        private static readonly object upgradeLock = new();
        private static readonly object updateGroupLock = new();
        private static readonly object upgradeGroupLock = new();
        private static readonly object entryLock = new();
        private static readonly object badsongsLock = new();
        private static readonly object invalidLock = new();
        private static readonly object errorLock = new();

        static CacheHandler() { }
        public ScanProgress Progress { get; protected set; }
        public int Count { get { lock (entryLock) return _count; } }
        public int NumScannedDirectories { get { lock (dirLock) return preScannedDirectories.Count; } }
        public int BadSongCount { get { lock (badsongsLock) return badSongs.Count; } }

        public readonly List<object> errorList = new();

        protected readonly SongCache cache;
        protected int _count;
        protected readonly HashSet<string> invalidSongsInCache = new();

        private readonly Dictionary<string, List<(string, DTAFileReader)>> updates = new();
        private readonly Dictionary<string, (DTAFileReader?, SongProUpgrade)> upgrades = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();
        private readonly SortedDictionary<string, ScanResult> badSongs = new();

        protected CacheHandler(SongCache cache) { this.cache = cache; }

        public bool QuickScan()
        {
            if (!LoadCacheFile_Quick())
            {
                ToastManager.ToastWarning("Song cache is not present or outdated - performing rescan");
                return false;
            }

            if (Count == 0)
            {
                ToastManager.ToastWarning("Song cache provided zero songs - performing rescan");
                return false;
            }

            SortCategories();
            return true;
        }

        public void FullScan(bool loadCache)
        {
            if (loadCache)
            {
                LoadCacheFile();
            }

            FindNewEntries();
            SortCategories();

            try
            {
                SaveToFile();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }

            try
            {
                WriteBadSongs();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex);
            }
        }

        protected abstract void FindNewEntries();
        protected abstract void SortCategories();
        protected abstract bool LoadCacheFile();
        protected abstract bool LoadCacheFile_Quick();

        protected void SaveToFile()
        {
            Progress = ScanProgress.WritingCache;
            using var writer = new BinaryWriter(new FileStream(CacheConstants.FILE, FileMode.Create, FileAccess.Write));
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            writer.Write(CacheConstants.VERSION);

            cache.titles.WriteToCache(writer, ref nodes);
            cache.artists.WriteToCache(writer, ref nodes);
            cache.albums.WriteToCache(writer, ref nodes);
            cache.genres.WriteToCache(writer, ref nodes);
            cache.years.WriteToCache(writer, ref nodes);
            cache.charters.WriteToCache(writer, ref nodes);
            cache.playlists.WriteToCache(writer, ref nodes);
            cache.sources.WriteToCache(writer, ref nodes);

            writer.Write(CacheConstants.baseDirectories.Length);
            foreach (string baseDirectory in CacheConstants.baseDirectories)
            {
                byte[] buffer = FormatIniEntriesToCache(baseDirectory, nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(cache.updateGroups.Count);
            foreach (var group in cache.updateGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            writer.Write(cache.upgradeGroups.Count);
            foreach (var group in cache.upgradeGroups)
            {
                byte[] buffer = group.FormatForCache();
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }

            List<KeyValuePair<string, PackedCONGroup>> upgradeCons = new();
            List<KeyValuePair<string, PackedCONGroup>> entryCons = new();
            foreach (var group in cache.conGroups)
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

            writer.Write(cache.extractedConGroups.Count);
            foreach (var group in cache.extractedConGroups)
            {
                byte[] buffer = group.Value.FormatEntriesForCache(group.Key, ref nodes);
                writer.Write(buffer.Length);
                writer.Write(buffer);
            }
        }

        protected void WriteBadSongs()
        {
            Progress = ScanProgress.WritingBadSongs;
#if UNITY_EDITOR
            string badSongsPath = Path.Combine(PathHelper.PersistentDataPath, "badsongs.txt");
#else
            string badSongsPath = Path.Combine(PathHelper.ExecutablePath, "badsongs.txt");
#endif
            using var stream = new FileStream(badSongsPath, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);

            foreach (var error in badSongs)
            {
                writer.WriteLineAsync(error.Key);
                switch (error.Value)
                {
                    case ScanResult.DirectoryError:
                        writer.WriteLineAsync("Error accessing directory contents");
                        break;
                    case ScanResult.IniEntryCorruption:
                        writer.WriteLineAsync("Corruption of either the ini file or chart/mid file");
                        break;
                    case ScanResult.NoName:
                        writer.WriteLineAsync("Name metadata not provided");
                        break;
                    case ScanResult.NoNotes:
                        writer.WriteLineAsync("No notes found");
                        break;
                    case ScanResult.DTAError:
                        writer.WriteLineAsync("Error occured while parsing DTA file node");
                        break;
                    case ScanResult.MissingMogg:
                        writer.WriteLineAsync("Required mogg audio file not present");
                        break;
                    case ScanResult.UnsupportedEncryption:
                        writer.WriteLineAsync("Mogg file uses unsupported encryption");
                        break;
                    case ScanResult.MissingMidi:
                        writer.WriteLineAsync("Midi file queried for found missing");
                        break;
                    case ScanResult.PossibleCorruption:
                        writer.WriteLineAsync("Possible corruption of a queried midi file");
                        break;
                }
                writer.WriteLineAsync();
            }
        }

        protected class DirectorySearcher
        {
            public readonly string?[] charts = new string?[3];
            public readonly string? ini = null;
            public readonly List<string> subfiles = new();

            public DirectorySearcher(string directory)
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
                        if (lowercase == CacheConstants.CHARTTYPES[i].Item1)
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
        }

        protected bool TraversalPreTest(string directory)
        {
            if (!FindOrMarkDirectory(directory) || (File.GetAttributes(directory) & FileAttributes.Hidden) != 0)
                return false;

            string filename = Path.GetFileName(directory);
            if (filename == "songs_updates")
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    CreateUpdateGroup(directory, dta);
                    return false;
                }
            }
            else if (filename == "song_upgrades")
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    CreateUpgradeGroup(directory, dta);
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

        protected bool ScanIniEntry(DirectorySearcher results)
        {
            for (int i = results.ini != null ? 0 : 2; i < 3; ++i)
            {
                var chart = results.charts[i];
                if (chart != null)
                {
                    try
                    {
                        using FrameworkFile_Alloc file = new(chart);
                        IniSongEntry entry = new(file, chart, results.ini, CacheConstants.CHARTTYPES[i].Item2);
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
                            AddErrors(e);
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
                        AddErrors(e);
                    }
                }
                reader.EndNode();
            }
        }

        protected void ReadIniEntry(string baseDirectory, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            string directory = Path.Combine(baseDirectory, reader.ReadLEBString());
            byte chartTypeIndex = reader.ReadByte();
            if (chartTypeIndex >= CacheConstants.CHARTTYPES.Length)
                return;

            ref var chartType = ref CacheConstants.CHARTTYPES[chartTypeIndex];
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

            if (CacheConstants.StartsWithBaseDirectory(directory))
            {
                FileInfo dta = new(Path.Combine(directory, "songs_updates.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    CreateUpdateGroup(directory, dta);

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

            if (CacheConstants.StartsWithBaseDirectory(directory))
            {
                FileInfo dta = new(Path.Combine(directory, "upgrades.dta"));
                if (dta.Exists)
                {
                    MarkDirectory(directory);
                    var group = CreateUpgradeGroup(directory, dta);

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

            if (CacheConstants.StartsWithBaseDirectory(filename) && CreateCONGroup(filename, out var group))
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
            if (!CacheConstants.StartsWithBaseDirectory(filename))
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
            if (!CacheConstants.StartsWithBaseDirectory(directory))
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
            if (chartTypeIndex >= CacheConstants.CHARTTYPES.Length)
                return;

            ref var chartType = ref CacheConstants.CHARTTYPES[chartTypeIndex];
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
            AddUpgradeGroup(group);

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
            if (!CacheConstants.StartsWithBaseDirectory(directory))
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

        protected void AddErrors(params object[] errors)
        {
            lock (errorLock) errorList.AddRange(errors);
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

        private bool CreateCONGroup(string filename, out PackedCONGroup? group)
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

        private void AddCONGroup(string filename, PackedCONGroup group)
        {
            lock (conLock) 
                cache.conGroups.Add(filename, group);
        }

        private void AddUpdateGroup(UpdateGroup group)
        {
            lock (updateGroupLock)
                cache.updateGroups.Add(group);
        }

        private void AddUpgradeGroup(UpgradeGroup group)
        {
            lock (upgradeGroupLock)
                cache.upgradeGroups.Add(group);
        }

        private void AddExtractedCONGroup(string directory, ExtractedConGroup group)
        {
            lock (extractedLock)
                cache.extractedConGroups.Add(directory, group);
        }

        private bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conLock)
                return cache.conGroups.TryGetValue(filename, out group);
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private void MarkFile(string file)
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
                if (cache.iniEntries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    cache.iniEntries.Add(hash, new() { entry });
            }
        }

        private void AddToBadSongs(string filePath, ScanResult err)
        {
            lock (badsongsLock) badSongs.Add(filePath, err);
        }

        private void CreateUpdateGroup(string directory, FileInfo dta, bool removeEntries = false)
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
                AddUpdateGroup(group);
        }

        private UpgradeGroup? CreateUpgradeGroup(string directory, FileInfo dta, bool removeEntries = false)
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
                AddUpgradeGroup(group);
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
                foreach (var group in cache.upgradeGroups)
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
                foreach (var group in cache.conGroups)
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
                foreach (var group in cache.conGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    cache.conGroups.Remove(entriesToRemove[i]);
            }

            lock (extractedLock)
            {
                List<string> entriesToRemove = new();
                foreach (var group in cache.extractedConGroups)
                {
                    group.Value.RemoveEntries(shortname);
                    if (group.Value.EntryCount == 0)
                        entriesToRemove.Add(group.Key);
                }

                for (int i = 0; i < entriesToRemove.Count; i++)
                    cache.extractedConGroups.Remove(entriesToRemove[i]);
            }
        }

        private bool AddEntry(SongEntry entry)
        {
            var hash = entry.Hash;
            lock (entryLock)
            {
                if (cache.entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    cache.entries.Add(hash, new() { entry });
                ++_count;
            }
            return true;
        }

        private byte[] FormatIniEntriesToCache(string baseDirectory, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<(string, IniSongEntry)> group = new();
            foreach (var node in cache.iniEntries)
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
