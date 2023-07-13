using YARG.Data;
using YARG.Song.Entries;

namespace YARG.Serialization.Parser
{
    public abstract class AbstractParser
    {
        protected SongEntry songEntry;

        public AbstractParser(SongEntry songEntry)
        {
            this.songEntry = songEntry;
        }

        public abstract void Parse(YargChart yargChart);
    }
}