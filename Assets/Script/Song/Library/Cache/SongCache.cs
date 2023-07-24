﻿using YARG.Hashes;
using YARG.Song.Library.CacheNodes;
using YARG.Serialization;
using YARG.Song.Entries;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TagLib.Riff;

#nullable enable
namespace YARG.Song.Library
{
    public partial class SongCache : IDisposable
    {
        private const int CACHE_VERSION = 23_07_02_01;
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

        static SongCache() { }

        private readonly List<UpdateGroup> updateGroups = new();
        private readonly Dictionary<string, List<(string, DTAFileReader)>> updates = new();
        private readonly List<UpgradeGroup> upgradeGroups = new();
        
        private readonly Dictionary<string, PackedCONGroup> conGroups = new();
        private readonly Dictionary<string, (DTAFileReader?, SongProUpgrade)> upgrades = new();
        private readonly Dictionary<string, ExtractedConGroup> extractedConGroups = new();
        private readonly Dictionary<Hash128Wrapper, List<IniSongEntry>> iniEntries = new();

        private int IniCount
        {
            get
            {
                int count = 0;
                foreach (var node in iniEntries)
                    count += node.Value.Count;
                return count;
            }
        }

        private readonly SongLibrary library = new();
        private readonly HashSet<string> preScannedDirectories = new();
        private readonly HashSet<string> preScannedFiles = new();

        internal readonly (string, ChartType)[] CHARTTYPES =
        {
            new("notes.mid",   ChartType.MID),
            new("notes.midi",  ChartType.MIDI),
            new("notes.chart", ChartType.CHART),
        };
        private bool disposedValue;

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

        private void FinalizeIniEntries()
        {
            foreach (var entryList in iniEntries)
                foreach (var entry in entryList.Value)
                    entry.FinishScan();
        }

        private void MapCategories()
        {
            Parallel.ForEach(library.entries, entryList =>
            {
                foreach (var entry in entryList.Value)
                {
                    library.titles.Add(entry);
                    library.artists.Add(entry);
                    library.albums.Add(entry);
                    library.genres.Add(entry);
                    library.years.Add(entry);
                    library.charters.Add(entry);
                    library.playlists.Add(entry);
                    library.sources.Add(entry);
                    library.artistAlbums.Add(entry);
                }
            });
        }

        private void MapCategories_Serial()
        {
            foreach (var entryList in library)
            {
                foreach (var entry in entryList.Value)
                {
                    library.titles.Add(entry);
                    library.artists.Add(entry);
                    library.albums.Add(entry);
                    library.genres.Add(entry);
                    library.years.Add(entry);
                    library.charters.Add(entry);
                    library.playlists.Add(entry);
                    library.sources.Add(entry);
                    library.artistAlbums.Add(entry);
                }
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
                conGroups.Add(filename, group);
        }

        private void AddExtractedCONGroup(string directory, ExtractedConGroup group)
        {
            lock (extractedLock)
                extractedConGroups.Add(directory, group);
        }

        private void AddIniEntry(Hash128Wrapper hash, IniSongEntry entry)
        {
            lock (iniLock)
            {
                if (iniEntries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    iniEntries.Add(hash, new() { entry });
            }
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
                    var lastWrite = file.LastWriteTime;
                    if (CanAddUpgrade(name, ref lastWrite))
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
                    var lastWrite = DateTime.FromBinary(listing.lastWrite);
                    if (CanAddUpgrade_CONInclusive(name, ref lastWrite))
                    {
                        SongProUpgrade upgrade = new(file, listing, lastWrite);
                        group.upgrades[name] = upgrade;
                        AddUpgrade(name, reader.Clone(), upgrade);
                        RemoveCONEntry(name);
                    }
                }

                reader.EndNode();
            }
        }

        private bool ProcessCONEntry(string name, ConSongEntry currentSong, out Hash128Wrapper? hash)
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

            return currentSong.Scan(out hash, name);
        }

        private bool CanAddUpgrade(string shortname, ref DateTime lastWrite)
        {
            lock (upgradeLock)
            {
                foreach (var group in upgradeGroups)
                {
                    if (group.upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade.UpgradeLastWrite >= lastWrite)
                            return false;
                        group.upgrades.Remove(shortname);
                        break;
                    }
                }
            }
            return true;
        }

        private bool CanAddUpgrade_CONInclusive(string shortname, ref DateTime lastWrite)
        {
            lock (conLock)
            {
                foreach (var group in conGroups)
                {
                    var upgrades = group.Value.upgrades;
                    if (upgrades.TryGetValue(shortname, out var currUpgrade))
                    {
                        if (currUpgrade!.UpgradeLastWrite >= lastWrite)
                            return false;
                        upgrades.Remove(shortname);
                        return true;
                    }
                }
            }

            return CanAddUpgrade(shortname, ref lastWrite);
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

        private bool AddEntry(Hash128Wrapper hash, SongEntry entry)
        {
            lock (entryLock)
            {
                if (library.entries.TryGetValue(hash, out var list))
                    list.Add(entry);
                else
                    library.entries.Add(hash, new() { entry });
            }
            return true;
        }

        private bool FindCONGroup(string filename, out PackedCONGroup? group)
        {
            lock (conLock)
                return conGroups.TryGetValue(filename, out group);
        }

        private void MarkDirectory(string directory)
        {
            lock (dirLock) preScannedDirectories.Add(directory);
        }

        private void MarkFile(string file)
        {
            lock (fileLock) preScannedFiles.Add(file);
        }
    }

    internal abstract class CONGroup
    {
        public class EntryNode
        {
            public readonly ConSongEntry entry;
            public readonly Hash128Wrapper hash;
            public EntryNode(ConSongEntry entry, Hash128Wrapper hash)
            {
                this.entry = entry;
                this.hash = hash;
            }
        }

        protected readonly Dictionary<string, SortedDictionary<int, EntryNode>> entries = new();
        protected readonly object entryLock = new();
        public int EntryCount
        {
            get
            {
                int count = 0;
                foreach (var node in entries)
                    count += node.Value.Count;
                return count;
            }
        }
        public void AddEntry(string name, int index, ConSongEntry entry, Hash128Wrapper hash)
        {
            EntryNode node = new(entry, hash);
            lock (entryLock)
            {
                if (entries.TryGetValue(name, out var dict))
                    dict.Add(index, node);
                else
                    entries.Add(name, new() { { index, node } });
            }
        }

        public void RemoveEntries(string name) { lock (entryLock) entries.Remove(name); }

        public void RemoveEntry(string name, int index) { lock (entryLock) entries[name].Remove(index); }

        public bool TryGetEntry(string name, int index, out EntryNode? entry)
        {
            entry = null;
            return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
        }

        protected void WriteEntriesToCache(BinaryWriter writer, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            writer.Write(EntryCount);
            foreach (var entryList in entries)
            {
                foreach (var entry in entryList.Value)
                {
                    writer.Write(entryList.Key);
                    writer.Write(entry.Key);

                    byte[] data = entry.Value.entry.FormatCacheData(nodes[entry.Value.entry]);
                    writer.Write(data.Length + 20);
                    writer.Write(data);

                    entry.Value.hash.Write(writer);
                }
            }
        }
    }

    internal class PackedCONGroup : CONGroup
    {
        public readonly CONFile file;
        public readonly DateTime lastWrite;
        public readonly Dictionary<string, SongProUpgrade> upgrades = new();
        
        public int UpgradeCount => upgrades.Count;
        
        private readonly object upgradeLock = new();

        private FileListing? songDTA;
        private FileListing? upgradeDta;
        public int DTALastWrite
        {
            get
            {
                if (songDTA == null)
                    return 0;
                return songDTA.lastWrite;
            }
        }
        public int UpgradeDTALastWrite
        {
            get
            {
                if (upgradeDta == null)
                    return 0;
                return upgradeDta.lastWrite;
            }
        }

        public PackedCONGroup(CONFile file, DateTime lastWrite)
        {
            this.file = file;
            this.lastWrite = lastWrite;
        }

        public void AddUpgrade(string name, SongProUpgrade upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }

        internal const string SONGSFILEPATH = "songs/songs.dta";
        internal const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

        public bool LoadUpgrades(out DTAFileReader? reader)
        {
            upgradeDta = file[UPGRADESFILEPATH];
            if (upgradeDta == null)
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(upgradeDta!)!, true);
            return true;
        }

        public bool LoadSongs(out DTAFileReader? reader)
        {
            if (songDTA == null && !SetSongDTA())
            {
                reader = null;
                return false;
            }

            reader = new(file.LoadSubFile(songDTA!)!, true);
            return true;
        }

        public bool SetSongDTA()
        {
            songDTA = file[SONGSFILEPATH];
            return songDTA != null;
        }

        public byte[] FormatUpgradesForCache(string filepath)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filepath);
            writer.Write(lastWrite.ToBinary());
            writer.Write(upgradeDta!.lastWrite);
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }

        public byte[] FormatEntriesForCache(string filepath, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(filepath);
            writer.Write(songDTA!.lastWrite);
            WriteEntriesToCache(writer, ref nodes);
            return ms.ToArray();
        }
    }

    internal class ExtractedConGroup : CONGroup
    {
        private readonly string dtaPath;
        private readonly DateTime lastWrite;

        public ExtractedConGroup(string dtaPath, DateTime lastWrite)
        {
            this.dtaPath = dtaPath;
            this.lastWrite = lastWrite;
        }

        public DTAFileReader? LoadDTA()
        {
            try
            {
                return new(dtaPath);
            }
            catch
            {
                return null;
            }
        }

        public byte[] FormatEntriesForCache(string directory, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(lastWrite.ToBinary());
            WriteEntriesToCache(writer, ref nodes);
            return ms.ToArray();
        }
    }

    internal class UpdateGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly List<string> updates = new();

        public UpdateGroup(string directory, DateTime dtaLastWrite)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] FormatForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(updates.Count);
            for (int i = 0; i < updates.Count; ++i)
                writer.Write(updates[i]);
            return ms.ToArray();
        }
    }

    internal class UpgradeGroup
    {
        public readonly string directory;
        private readonly DateTime dtaLastWrite;
        public readonly Dictionary<string, SongProUpgrade> upgrades = new();

        public UpgradeGroup(string directory, DateTime dtaLastWrite)
        {
            this.directory = directory;
            this.dtaLastWrite = dtaLastWrite;
        }

        public byte[] FormatForCache()
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dtaLastWrite.ToBinary());
            writer.Write(upgrades.Count);
            foreach (var upgrade in upgrades)
            {
                writer.Write(upgrade.Key);
                upgrade.Value.WriteToCache(writer);
            }
            return ms.ToArray();
        }
    }
}
