using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Data;
using YARG.Input;
using YARG.Song.Entries;
using YARG.UI.MusicLibrary.ViewTypes;
using Random = UnityEngine.Random;
using YARG.Types;
using static YARG.Song.SongSorting;

namespace YARG.Song
{
    public class SongSearching
    {
        private static readonly List<(string, string)> SearchLeniency = new()
        {
            ("Æ", "AE") // Tool - Ænema
        };

        private class FilterNode : IEquatable<FilterNode>
        {
            public readonly SongAttribute attribute;
            public readonly string argument;
            public FilterNode(SongAttribute attribute, string argument)
            {
                this.attribute = attribute;
                this.argument = argument;
            }

            public override bool Equals(object o)
            {
                return o is FilterNode node && Equals(node);
            }

            public bool Equals(FilterNode other)
            {
                return attribute == other.attribute && argument == other.argument;
            }

            public override int GetHashCode()
            {
                return attribute.GetHashCode() ^ argument.GetHashCode();
            }

            public static bool operator==(FilterNode left, FilterNode right)
            {
                return left.Equals(right);
            }

            public static bool operator!=(FilterNode left, FilterNode right)
            {
                return !left.Equals(right);
            }

            public bool StartsWith(FilterNode other)
            {
                return attribute == other.attribute && argument.StartsWith(other.argument);
            }
        }

        private List<(FilterNode, FlatMap<string, List<SongEntry>>)> filters = new();

        public FlatMap<string, List<SongEntry>> Search(string value, ref SongAttribute sort)
        {
            var currentFilters = GetFilters(value.Split(';'));
            if (currentFilters.Count == 0)
            {
                filters.Clear();
                return SongContainer.GetSongList(sort);
            }

            int currFilterIndex = 0;
            int prevFilterIndex = 0;
            while (currFilterIndex < currentFilters.Count && prevFilterIndex < filters.Count && currentFilters[currFilterIndex].StartsWith(filters[prevFilterIndex].Item1))
            {
                do
                {
                    ++prevFilterIndex;
                } while (prevFilterIndex < filters.Count && currentFilters[currFilterIndex].StartsWith(filters[prevFilterIndex].Item1));
                if (currentFilters[currFilterIndex] != filters[prevFilterIndex - 1].Item1)
                    break;
                ++currFilterIndex;
            }

            if (currFilterIndex == 0 && (prevFilterIndex == 0 || currentFilters[0].attribute != SongAttribute.UNSPECIFIED))
            {
                if (currentFilters[0].attribute != SongAttribute.UNSPECIFIED)
                    sort = currentFilters[0].attribute;

                if (prevFilterIndex < filters.Count)
                    filters[prevFilterIndex] = new(currentFilters[0], SearchSongs(currentFilters[0]));
                else
                    filters.Add(new(currentFilters[0], SearchSongs(currentFilters[0])));

                ++prevFilterIndex;
                ++currFilterIndex;
            }

            while (currFilterIndex < currentFilters.Count)
            {
                var filter = currentFilters[currFilterIndex];
                var searchList = SearchSongs(filter, Clone(filters[prevFilterIndex - 1].Item2));

                if (prevFilterIndex < filters.Count)
                    filters[prevFilterIndex] = new(filter, searchList);
                else
                    filters.Add(new(filter, searchList));

                ++currFilterIndex;
                ++prevFilterIndex;
            }

            if (prevFilterIndex < filters.Count)
                filters.RemoveRange(prevFilterIndex, filters.Count - prevFilterIndex);
            return filters[prevFilterIndex - 1].Item2;
        }

        private static FlatMap<string, List<SongEntry>> Clone(FlatMap<string, List<SongEntry>> original)
        {
            FlatMap<string, List<SongEntry>> clone = new();
            for (int i = 0; i < original.Count; ++i)
            {
                var node = original.At_index(i);
                clone.Add(node.key, new(node.obj));
            }
            return clone;
        }

        public static string RemoveWhitespace(string str)
        {
            int index = 0;
            while (index < str.Length && str[index] <= 32)
                ++index;

            if (index == str.Length)
                return string.Empty;
            return index > 0 ? str[index..] : str;
        }

        private static List<FilterNode> GetFilters(string[] split)
        {
            List<FilterNode> nodes = new();
            foreach (string arg in split)
            {
                SongAttribute attribute;
                string argument = RemoveWhitespace(arg);
                if (argument == string.Empty)
                    continue;

                if (argument.StartsWith("artist:"))
                {
                    attribute = SongAttribute.ARTIST;
                    argument = RemoveDiacriticsAndArticle(argument[7..]);
                }
                else if (argument.StartsWith("source:"))
                {
                    attribute = SongAttribute.SOURCE;
                    argument = argument[7..].ToLower();
                }
                else if (argument.StartsWith("album:"))
                {
                    attribute = SongAttribute.ALBUM;
                    argument = RemoveDiacritics(argument[6..]);
                }
                else if (argument.StartsWith("charter:"))
                {
                    attribute = SongAttribute.CHARTER;
                    argument = argument[8..].ToLower();
                }
                else if (argument.StartsWith("year:"))
                {
                    attribute = SongAttribute.YEAR;
                    argument = argument[5..].ToLower();
                }
                else if (argument.StartsWith("genre:"))
                {
                    attribute = SongAttribute.GENRE;
                    argument = argument[6..].ToLower();
                }
                else if (argument.StartsWith("name:"))
                {
                    attribute = SongAttribute.TITLE;
                    argument = RemoveDiacriticsAndArticle(argument[5..]);
                }
                else if (argument.StartsWith("title:"))
                {
                    attribute = SongAttribute.TITLE;
                    argument = RemoveDiacriticsAndArticle(argument[6..]);
                }
                else
                {
                    attribute = SongAttribute.UNSPECIFIED;
                    argument = RemoveDiacritics(argument);
                }

                argument = RemoveWhitespace(argument!);
                nodes.Add(new(attribute, argument));
                if (attribute == SongAttribute.UNSPECIFIED)
                    break;
            }
            return nodes;
        }

        private static FlatMap<string, List<SongEntry>> SearchSongs(FilterNode arg)
        {
            if (arg.attribute == SongAttribute.UNSPECIFIED)
            {
                var results = SongContainer.Songs.Select(i => new
                {
                    score = Search(arg.argument, i),
                    songInfo = i
                })
                .Where(i => i.score >= 0)
                .OrderBy(i => i.score)
                .Select(i => i.songInfo).ToList();
                return new() { { "Search Results" , results } };
            }

            return SongContainer.Search(arg.attribute, arg.argument);
        }

        private static FlatMap<string, List<SongEntry>> SearchSongs(FilterNode arg, FlatMap<string, List<SongEntry>> searchList)
        {
            if (arg.attribute == SongAttribute.UNSPECIFIED)
            {
                List<SongEntry> entriesToSearch = new();
                for (int i = 0; i < searchList.Count; ++i)
                    entriesToSearch.AddRange(searchList.At_index(i).obj);

                var results = entriesToSearch.Select(i => new
                {
                    score = Search(arg.argument, i),
                    songInfo = i
                })
                .Where(i => i.score >= 0)
                .OrderBy(i => i.score)
                .Select(i => i.songInfo).ToList();
                searchList = new() { { "Search Results", results } };
            }
            else
            {
                Predicate<SongEntry> match = arg.attribute switch
                {
                    SongAttribute.TITLE => entry => !RemoveArticle(entry.Name.SortStr).Contains(arg.argument),
                    SongAttribute.ARTIST => entry => !RemoveArticle(entry.Artist.SortStr).Contains(arg.argument),
                    SongAttribute.ALBUM => entry => !entry.Album.SortStr.Contains(arg.argument),
                    SongAttribute.GENRE => entry => !entry.Genre.SortStr.Contains(arg.argument),
                    SongAttribute.YEAR => entry => !entry.Year.Contains(arg.argument) && !entry.UnmodifiedYear.Contains(arg.argument),
                    SongAttribute.CHARTER => entry => !entry.Charter.SortStr.Contains(arg.argument),
                    SongAttribute.PLAYLIST => entry => !entry.Playlist.SortStr.Contains(arg.argument),
                    SongAttribute.SOURCE => entry => !entry.Source.SortStr.Contains(arg.argument),
                    _ => throw new Exception("HOW")
                };

                for (int i = 0; i < searchList.Count;)
                {
                    var node = searchList.At_index(i);
                    node.obj.RemoveAll(match);
                    if (node.obj.Count == 0)
                        searchList.RemoveAt(i);
                    else
                        ++i;
                }
            }
            return searchList;
        }

        public static string RemoveDiacritics(string text)
        {
            if (text == null)
            {
                return null;
            }

            foreach (var c in SearchLeniency)
            {
                text = text.Replace(c.Item1, c.Item2);
            }

            var normalizedString = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder(capacity: normalizedString.Length);

            foreach (char c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder
                .ToString()
                .Normalize(NormalizationForm.FormC);
        }

        public static string RemoveDiacriticsAndArticle(string text)
        {
            var textWithoutDiacritics = RemoveDiacritics(text);
            return SongSorting.RemoveArticle(textWithoutDiacritics);
        }

        private static int Search(string input, SongEntry songInfo)
        {
            // Get name index
            string name = songInfo.Name.SortStr;
            int nameIndex = name.IndexOf(input, StringComparison.Ordinal);

            // Get artist index
            string artist = songInfo.Artist.SortStr;
            int artistIndex = artist.IndexOf(input, StringComparison.Ordinal);

            // Return the best search
            if (nameIndex == -1 && artistIndex == -1)
            {
                return -1;
            }

            if (nameIndex == -1)
            {
                return artistIndex;
            }

            if (artistIndex == -1)
            {
                return nameIndex;
            }

            return Mathf.Min(nameIndex, artistIndex);
        }
    }
}
