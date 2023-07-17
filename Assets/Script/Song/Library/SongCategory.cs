using YARG.Song.Entries;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

namespace YARG.Song.Library
{
    public abstract class SongCategory<Key>
        where Key : IComparable<Key>, IEquatable<Key>
    {
        protected readonly object elementLock = new();
        protected readonly FlatMap<Key, List<SongEntry>> elements = new();

        public abstract void Add(SongEntry entry);

        public FlatMap<Key, List<SongEntry>>.Enumerator GetEnumerator() { return (FlatMap<Key, List<SongEntry>>.Enumerator)elements.GetEnumerator(); }

        public abstract FlatMap<Key, List<SongEntry>> GetSongSelectionList();
    }

    public abstract class SerializableCategory<Key> : SongCategory<Key>
        where Key : IComparable<Key>, IEquatable<Key>
    {
        protected readonly SongAttribute attribute;
        protected readonly EntryComparer comparer;

        public SerializableCategory(SongAttribute attribute)
        {
            switch (attribute)
            {
                case SongAttribute.UNSPECIFIED:
                case SongAttribute.SONG_LENGTH:
                    throw new Exception("stoopid");
            }

            this.attribute = attribute;
            comparer = new(attribute);
        }

        protected void Add(Key key, SongEntry entry)
        {
            lock (elementLock)
            {
                var node = elements[key];
                int index = node.BinarySearch(entry, comparer);
                node.Insert(~index, entry);
            }
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> strings = new();
            int index = -1;
            foreach (var element in this)
            {
                foreach (var entry in element.obj)
                {
                    string str = entry.GetStringAttribute(attribute);
                    if (index == -1 || strings[index] != str)
                    {
                        strings.Add(str);
                        index++;
                    }

                    CategoryCacheWriteNode node;
                    if (attribute == SongAttribute.TITLE)
                        node = nodes[entry] = new();
                    else
                        node = nodes[entry];
                    switch (attribute)
                    {
                        case SongAttribute.TITLE: node.title = index; break;
                        case SongAttribute.ARTIST: node.artist = index; break;
                        case SongAttribute.ALBUM: node.album = index; break;
                        case SongAttribute.GENRE: node.genre = index; break;
                        case SongAttribute.YEAR: node.year = index; break;
                        case SongAttribute.CHARTER: node.charter = index; break;
                        case SongAttribute.PLAYLIST: node.playlist = index; break;
                        case SongAttribute.SOURCE: node.source = index; break;
                    }
                }
            }

            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);
            writer.Write(index + 1);
            foreach (string str in strings)
                writer.Write(str);

            fileWriter.Write((int) ms.Length);
            ms.WriteTo(fileWriter.BaseStream);
        }
    }

    public class MainCategory : SerializableCategory<SortString>
    {
        public MainCategory(SongAttribute attribute) : base(attribute)
        {
            if (attribute == SongAttribute.TITLE || attribute == SongAttribute.YEAR)
                throw new Exception("Use the dedicated category for this metadata type");
        }

        public override void Add(SongEntry entry)
        {
            Add(entry.GetStringAttribute(attribute), entry);
        }

        public override FlatMap<SortString, List<SongEntry>> GetSongSelectionList()
        {
            return elements;
        }
    }

    public class TitleCategory : SerializableCategory<string>
    {
        public TitleCategory() : base(SongAttribute.TITLE) { }

        public override void Add(SongEntry entry)
        {
            string name = entry.Name.SortStr;
            int i = 0;
            while (i + 1 < name.Length && !char.IsLetterOrDigit(name[i]))
                ++i;

            char character = name[i];
            if (char.IsDigit(character))
                Add("0-9", entry);
            else
                Add(char.ToUpper(character).ToString(), entry);
        }

        public override FlatMap<string, List<SongEntry>> GetSongSelectionList()
        {
            FlatMap<string, List<SongEntry>> map = new();
            foreach (var element in this)
                map.Add(element.key, element.obj);
            return map;
        }
    }

    public class YearCategory : SerializableCategory<string>
    {
        public YearCategory() : base(SongAttribute.YEAR) { }

        public override void Add(SongEntry entry)
        {
            if (entry.YearAsNumber != int.MaxValue)
                Add(entry.Year[..3] + "0s", entry);
            else
                Add(entry.Year, entry);
        }

        public override FlatMap<string, List<SongEntry>> GetSongSelectionList()
        {
            FlatMap<string, List<SongEntry>> map = new();
            foreach (var element in this)
                map.Add(element.key, element.obj);
            return map;
        }
    }

    public class ArtistAlbumCategory : SongCategory<string>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.ALBUM);
        public override void Add(SongEntry entry)
        {
            string key = $"{entry.Artist.Str} - {entry.Album.Str}";
            lock (elementLock)
            {
                var node = elements[key];
                int index = node.BinarySearch(entry, comparer);
                node.Insert(~index, entry);
            }
        }

        public override FlatMap<string, List<SongEntry>> GetSongSelectionList()
        {
            return elements;
        }
    }

    public class SongLengthCategory : SongCategory<string>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.SONG_LENGTH);
        public override void Add(SongEntry entry)
        {
            string key = entry.SongLengthTimeSpan.TotalMinutes switch
            {
                <= 0.00 => "-",
                <= 2.00 => "00:00 - 02:00",
                <= 5.00 => "02:00 - 05:00",
                <= 10.00 => "05:00 - 10:00",
                <= 15.00 => "10:00 - 15:00",
                <= 20.00 => "15:00 - 20:00",
                _ => "20:00+",
            };

            lock (elementLock)
            {
                var node = elements[key];
                int index = node.BinarySearch(entry, comparer);
                node.Insert(~index, entry);
            }
        }

        public override FlatMap<string, List<SongEntry>> GetSongSelectionList()
        {
            return elements;
        }
    }

    public class InstrumentCategory
    {
        private readonly NoteTrackType instrument;
        private readonly string name;
        private readonly InstrumentComparer comparer;
        private readonly List<SongEntry> elements = new();
        private readonly object elementLock = new();

        public InstrumentCategory(NoteTrackType instrument)
        {
            this.instrument = instrument;
            name = instrument switch
            {
                NoteTrackType.Lead => "Lead",
                NoteTrackType.Lead_6 => "Lead GHL",
                NoteTrackType.Bass => "Bass",
                NoteTrackType.Bass_6 => "Bass GHL",
                NoteTrackType.Rhythm => "Rhythm",
                NoteTrackType.Coop => "Coop",
                NoteTrackType.Keys => "Keys",
                NoteTrackType.Drums_4 => "Drums",
                NoteTrackType.Drums_4Pro => "Pro Drums",
                NoteTrackType.Drums_5 => "GH Drums",
                NoteTrackType.Vocals => "Vocals",
                NoteTrackType.Harmonies => "Harmonies",
                NoteTrackType.ProGuitar_17 => "Pro Guitar 17-Fret",
                NoteTrackType.ProGuitar_22 => "Pro Guitar 22-Fret",
                NoteTrackType.ProBass_17 => "Pro Bass 17-Fret",
                NoteTrackType.ProBass_22 => "Pro Bass 22-Fret",
                NoteTrackType.ProKeys => "Pro Keys",
                _ => throw new ArgumentException(nameof(instrument)),
            };
            comparer = new InstrumentComparer(instrument);
        }

        public void Add(SongEntry entry)
        {
            if (entry.GetValues(instrument).subTracks == 0)
                return;

            lock (elementLock)
            {
                int index = elements.BinarySearch(entry, comparer);
                elements.Insert(~index, entry);
            }
        }

        public FlatMap<string, List<SongEntry>> GetSongSelectionList()
        {
            return new() { { name, elements } };
        }

        public FlatMap<string, List<SongEntry>> GetSongSelectionList_Clone()
        {
            return new() { { name, new(elements) } };
        }
    }
}
