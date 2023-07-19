using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Assets.Script.Types;
using YARG.Serialization;
using YARG.Song.Entries;

#nullable enable
namespace YARG.Song.Library
{
    public abstract class CONGroup
    {
        protected readonly Dictionary<string, SortedDictionary<int, ConSongEntry>> entries = new();
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
        public abstract void ReadEntry(string nodeName, int index, BinaryFileReader reader, CategoryCacheStrings strings);

        public void AddEntry(string name, int index, ConSongEntry entry)
        {
            lock (entryLock)
            {
                if (entries.TryGetValue(name, out var dict))
                    dict.Add(index, entry);
                else
                    entries.Add(name, new() { { index, entry } });
            }
        }

        public void RemoveEntries(string name) { lock (entryLock) entries.Remove(name); }

        public void RemoveEntry(string name, int index) { lock (entryLock) entries[name].Remove(index); }

        public bool TryGetEntry(string name, int index, out ConSongEntry? entry)
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

                    byte[] data = entry.Value.FormatCacheData(nodes[entry.Value]);
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }
        }
    }

    public class PackedCONGroup : CONGroup
    {
        public const string SONGSFILEPATH = "songs/songs.dta";
        public const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";

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

        public override void ReadEntry(string nodeName, int index, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            var midiListing = file[reader.ReadLEBString()];
            if (midiListing == null || midiListing.lastWrite != reader.ReadInt32())
                return;

            FileListing? moggListing = null;
            AbridgedFileInfo? moggInfo = null;
            if (reader.ReadBoolean())
            {
                moggListing = file[reader.ReadLEBString()];
                if (moggListing == null || moggListing.lastWrite != reader.ReadInt32())
                    return;
            }
            else
            {
                FileInfo info = new(reader.ReadLEBString());
                if (!info.Exists || info.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
                moggInfo = info;
            }

            AbridgedFileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                FileInfo info = new(reader.ReadLEBString());
                if (!info.Exists || info.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
                updateInfo = info;
            }

            ConSongEntry currentSong = new(file, nodeName, midiListing, moggListing, moggInfo, updateInfo, reader, strings);
            AddEntry(nodeName, index, currentSong);
        }

        public void AddUpgrade(string name, SongProUpgrade upgrade) { lock (upgradeLock) upgrades[name] = upgrade; }

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

    public class ExtractedConGroup : CONGroup
    {
        public readonly AbridgedFileInfo dta;
        public readonly DTAFileReader reader;

        public ExtractedConGroup(FileInfo dta)
        {
            this.dta = dta;
            reader = new(dta.FullName);
        }

        public override void ReadEntry(string nodeName, int index, BinaryFileReader reader, CategoryCacheStrings strings)
        {
            FileInfo midiInfo = new(reader.ReadLEBString());
            if (!midiInfo.Exists || midiInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            bool isYargMogg = reader.ReadBoolean();
            FileInfo moggInfo = new(reader.ReadLEBString());
            if (!moggInfo.Exists || moggInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                return;

            FileInfo? updateInfo = null;
            if (reader.ReadBoolean())
            {
                updateInfo = new FileInfo(reader.ReadLEBString());
                if (!updateInfo.Exists || updateInfo.LastWriteTime != DateTime.FromBinary(reader.ReadInt64()))
                    return;
            }

            ConSongEntry currentSong;
            if (isYargMogg)
                currentSong = new(midiInfo, dta, moggInfo, null, updateInfo, reader, strings);
            else
                currentSong = new(midiInfo, dta, null, moggInfo, updateInfo, reader, strings);
            AddEntry(nodeName, index, currentSong);
        }

        public byte[] FormatEntriesForCache(string directory, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(directory);
            writer.Write(dta.LastWriteTime.ToBinary());
            WriteEntriesToCache(writer, ref nodes);
            return ms.ToArray();
        }
    }

    public class UpdateGroup
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

    public class UpgradeGroup
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
