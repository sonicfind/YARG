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
    public class CategoryNode
    {
        private readonly List<SongEntry> entries = new();
        public void Add(SongEntry entry, EntryComparer comparer)
        {
            int index = entries.BinarySearch(entry, comparer);
            entries.Insert(~index, entry);
        }

        public List<SongEntry>.Enumerator GetEnumerator() => entries.GetEnumerator();
    }

    public abstract class SongCategory<Key, Element>
        where Element : new()
        where Key : IComparable<Key>, IEquatable<Key>
    {
        protected readonly object elementLock = new();
        protected readonly FlatMap<Key, Element> elements = new();

        public abstract void Add(SongEntry entry);

        public FlatMap<Key, Element>.Enumerator GetEnumerator() { return elements.GetEnumerator(); }
    }

    public abstract class SerializableCategory<Key> : SongCategory<Key, CategoryNode>
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
            lock (elementLock) elements[key].Add(entry, comparer);
        }

        public void WriteToCache(BinaryWriter fileWriter, ref Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            List<string> strings = new();
            int index = -1;
            foreach (var element in elements)
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
                Add(character.ToString(), entry);
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
    }

    public class ArtistAlbumCategory : SongCategory<SortString, FlatMap<SortString, CategoryNode>>
    {
        private static readonly EntryComparer comparer = new(SongAttribute.ALBUM);
        public override void Add(SongEntry entry)
        {
            lock (elementLock) elements[entry.Artist][entry.Album].Add(entry, comparer);
        }
    }

    public class SongLengthCategory : SongCategory<string, CategoryNode>
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
            lock (elementLock) elements[key].Add(entry, comparer);
        }
    }
}
