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

namespace YARG.Song
{
    public static class SongSearching
    {
        private static readonly List<(string, string)> SearchLeniency = new()
        {
            ("Æ", "AE") // Tool - Ænema
        };

        // TODO: Make search query separate. This gets rid of the need of a tuple in SearchSongs
        public static SortedSongList Search(string value, SongSorting.Sort sort)
        {
            if (string.IsNullOrEmpty(value))
                return SongSorting.GetSortCache(sort);

            var songsOut = new List<SongEntry>(SongContainer.Songs);
            bool searching = true;

            var split = value.Split(';');
            foreach (var arg in split)
            {
                if (SearchSongs(arg, ref songsOut))
                {
                    searching = true;
                    break;
                }

                if (songsOut.Count == 0)
                    break;
            }

            SortedSongList sortedSongs;
            if (searching)
            {
                sortedSongs = new SortedSongList("Search Results", songsOut);
            }
            else
            {
                sortedSongs = new SortedSongList(SongSorting.GetSortCache(sort));
                sortedSongs.Intersect(songsOut);
            }

            return sortedSongs;
        }

        private static bool SearchSongs(string arg, ref List<SongEntry> songs)
        {
            if (arg.StartsWith("artist:"))
            {
                string artist = RemoveDiacriticsAndArticle(arg[7..]);
                songs.RemoveAll(entry => SongSorting.RemoveArticle(entry.Artist.SortStr) != artist);
            }

            else if (arg.StartsWith("source:"))
            {
                string source = arg[7..].ToLower();
                songs.RemoveAll(entry => entry.Source.SortStr != source);
            }

            else if (arg.StartsWith("album:"))
            {
                string album = RemoveDiacritics(arg[6..]);
                songs.RemoveAll(entry => entry.Album.SortStr != album);
            }

            else if (arg.StartsWith("charter:"))
            {
                string charter = arg[8..].ToLower();
                songs.RemoveAll(entry => entry.Charter.SortStr != charter);
            }

            else if (arg.StartsWith("year:"))
            {
                string year = arg[5..].ToLower();
                songs.RemoveAll(entry => entry.Year != year && entry.UnmodifiedYear != year);
            }

            else if (arg.StartsWith("genre:"))
            {
                string genre = arg[6..].ToLower();
                songs.RemoveAll(entry => entry.Genre.SortStr != genre);
            }

            else if (arg.StartsWith("instrument:"))
            {
                var instrument = arg[11..];

                if (!string.IsNullOrEmpty(instrument) && instrument.Length > 1 && instrument.StartsWith("~"))
                {
                    instrument = instrument[1..];
                    if (instrument == "band")
                    {
                        songs.RemoveAll(entry => entry.BandDifficulty >= 0);
                    }
                    else
                    {
                        var ins = InstrumentHelper.FromStringName(instrument);
                        songs.RemoveAll(entry => entry.HasInstrument(ins));
                    }
                }
                else if (instrument == "band")
                {
                    songs.RemoveAll(entry => entry.BandDifficulty < 0);
                }
                else
                {
                    var ins = InstrumentHelper.FromStringName(instrument);
                    songs.RemoveAll(entry => !entry.HasInstrument(ins));
                }
            }
            else
            {
                var tmp = songs.Select(i => new
                {
                    score = Search(arg, i),
                    songInfo = i
                })
                .Where(i => i.score >= 0)
                .OrderBy(i => i.score)
                .Select(i => i.songInfo).ToList();
                songs = tmp;
                return true;
            }
            return false;
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
            string normalizedInput = RemoveDiacritics(input);

            // Get name index
            string name = songInfo.Name.SortStr;
            int nameIndex = name.IndexOf(normalizedInput, StringComparison.Ordinal);

            // Get artist index
            string artist = songInfo.Artist;
            int artistIndex = artist.IndexOf(normalizedInput, StringComparison.Ordinal);

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
