using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using YARG.Song.Entries;
using YARG.Types;
using TagLib;
using UnityEngine;

namespace YARG.Song
{
    public class SongSorting
    {
        private FlatMap<string, List<SongEntry>> Titles;
        private FlatMap<string, List<SongEntry>> Years;
        private FlatMap<string, List<SongEntry>> ArtistAlbums;
        private FlatMap<string, List<SongEntry>> SongLengths;
        private FlatMap<string, List<SongEntry>> Artists;
        private FlatMap<string, List<SongEntry>> Albums;
        private FlatMap<string, List<SongEntry>> Genres;
        private FlatMap<string, List<SongEntry>> Charters;
        private FlatMap<string, List<SongEntry>> Playlists;
        private FlatMap<string, List<SongEntry>> Sources;

        public SongSorting()
        {
            Titles = new();
            Years = new();
            ArtistAlbums = new();
            SongLengths = new();
            Artists = new();
            Albums = new();
            Genres = new();
            Charters = new();
            Playlists = new();
            Sources = new();
        }

        public SongSorting(SongContainer container)
        {
            Titles = container.Titles;
            Years = container.Years;
            ArtistAlbums = container.ArtistAlbums;
            SongLengths = container.SongLengths;
            Artists = Convert(container.Artists);
            Albums = Convert(container.Albums);
            Genres = Convert(container.Genres);
            Charters = Convert(container.Charters);
            Playlists = Convert(container.Playlists);
            Sources = Convert(container.Sources);

            static FlatMap<string, List<SongEntry>> Convert(FlatMap<SortString, List<SongEntry>> list)
            {
                FlatMap<string, List<SongEntry>> map = new();
                foreach (FlatMapNode<SortString, List<SongEntry>> node in list)
                    map.Add(node.key, node.obj);
                return map;
            }
        }

        public FlatMap<string, List<SongEntry>> GetSongList(SongAttribute sort)
        {
            return sort switch
            {
                SongAttribute.TITLE => Titles,
                SongAttribute.ARTIST => Artists,
                SongAttribute.ALBUM => Albums,
                SongAttribute.GENRE => Genres,
                SongAttribute.YEAR => Years,
                SongAttribute.CHARTER => Charters,
                SongAttribute.PLAYLIST => Playlists,
                SongAttribute.SOURCE => Sources,
                SongAttribute.ARTIST_ALBUM => ArtistAlbums,
                SongAttribute.SONG_LENGTH => SongLengths,
                _ => throw new Exception("stoopid"),
            };
        }
    }
}