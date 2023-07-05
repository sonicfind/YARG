using YARG.Hashes;
using System.Collections.Generic;
using YARG.Song.Entries;

namespace YARG.Song.Library
{
    public class SongLibrary
    {
        public readonly Dictionary<Hash128Wrapper, List<SongEntry>> entries = new();
        public readonly TitleCategory titles = new();
        public readonly ArtistCategory artists = new();
        public readonly AlbumCategory albums = new();
        public readonly GenreCategory genres = new();
        public readonly YearCategory years = new();
        public readonly CharterCategory charters = new();
        public readonly PlaylistCategory playlists = new();
        public readonly SourceCategory sources = new();
        public readonly ArtistAlbumCategory artistAlbums = new();

        public int Count
        {
            get
            {
                int count = 0;
                foreach(var node in entries)
                    count += node.Value.Count;
                return count;
            }
        }

        public Dictionary<Hash128Wrapper, List<SongEntry>>.Enumerator GetEnumerator() => entries.GetEnumerator();
    }
}
