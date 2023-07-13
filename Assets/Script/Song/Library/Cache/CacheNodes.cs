using YARG.Serialization;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Library
{
    public class CategoryCacheWriteNode
    {
        public int title;
        public int artist;
        public int album;
        public int genre;
        public int year;
        public int charter;
        public int playlist;
        public int source;
    }

    public abstract class CategoryCacheStrings
    {
        public string[] titles = Array.Empty<string>();
        public string[] artists = Array.Empty<string>();
        public string[] albums = Array.Empty<string>();
        public string[] genres = Array.Empty<string>();
        public string[] years = Array.Empty<string>();
        public string[] charters = Array.Empty<string>();
        public string[] playlists = Array.Empty<string>();
        public string[] sources = Array.Empty<string>();

        protected static string[] ReadStrings(BinaryFileReader reader)
        {
            int count = reader.ReadInt32();
            string[] strings = new string[count];
            for (int i = 0; i < count; ++i)
                strings[i] = reader.ReadLEBString();
            return strings;
        }
    }

    public class CategoryCacheStrings_Serial : CategoryCacheStrings
    {
        public CategoryCacheStrings_Serial(BinaryFileReader reader)
        {
            int bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            titles = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            artists = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            albums = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            genres = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            years = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            charters = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            playlists = ReadStrings(reader);
            reader.ExitSection();

            bytes = reader.ReadInt32();
            reader.EnterSection(bytes);
            sources = ReadStrings(reader);
            reader.ExitSection();
        }
    }

    public class CategoryCacheStrings_Parallel : CategoryCacheStrings
    {
        public CategoryCacheStrings_Parallel(BinaryFileReader reader)
        {
            var tasks = new Task[8];

            int bytes = reader.ReadInt32();
            var titleReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[0] = Task.Run(() => { titles = ReadStrings(titleReader); titleReader.Dispose(); });

            bytes = reader.ReadInt32();
            var artistReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[1] = Task.Run(() => { artists = ReadStrings(artistReader); artistReader.Dispose(); });

            bytes = reader.ReadInt32();
            var albumReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[2] = Task.Run(() => { albums = ReadStrings(albumReader); albumReader.Dispose(); });

            bytes = reader.ReadInt32();
            var genreReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[3] = Task.Run(() => { genres = ReadStrings(genreReader); genreReader.Dispose(); });

            bytes = reader.ReadInt32();
            var yearReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[4] = Task.Run(() => { years = ReadStrings(yearReader); yearReader.Dispose(); });

            bytes = reader.ReadInt32();
            var charterReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[5] = Task.Run(() => { charters = ReadStrings(charterReader); charterReader.Dispose(); });

            bytes = reader.ReadInt32();
            var playlistReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[6] = Task.Run(() => { playlists = ReadStrings(playlistReader); playlistReader.Dispose(); });

            bytes = reader.ReadInt32();
            var sourceReader = reader.CreateReaderFromCurrentPosition(bytes);
            tasks[7] = Task.Run(() => { sources = ReadStrings(sourceReader); sourceReader.Dispose(); });
            Task.WaitAll(tasks);
        }
    }
}
