using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Entries
{
    public class SongProUpgrade
    {
        public DateTime UpgradeLastWrite { get; protected set; }
#nullable enable
        private readonly CONFile? conFile;
        public FileListing? UpgradeMidiListing { get; private set; }
        public string UpgradeMidiPath { get; private set; } = string.Empty;

        public SongProUpgrade(CONFile? conFile, FileListing? listing, DateTime lastWrite)
        {
            this.conFile = conFile;
            UpgradeMidiListing = listing;
            UpgradeLastWrite = lastWrite;
        }

        public SongProUpgrade(string filename, DateTime lastWrite)
        {
            UpgradeMidiPath = filename;
            UpgradeLastWrite = lastWrite;
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(UpgradeLastWrite.ToBinary());
        }

        public FrameworkFile? LoadUpgradeMidi()
        {
            if (UpgradeMidiPath == string.Empty)
            {
                if (conFile == null || UpgradeMidiListing == null)
                    return null;
                return new FrameworkFile_Pointer(conFile.LoadSubFile(UpgradeMidiListing)!, true);
            }

            FileInfo info = new(UpgradeMidiPath);
            if (!info.Exists || info.LastWriteTime != UpgradeLastWrite)
                return null;
            return new FrameworkFile_Alloc(UpgradeMidiPath);
        }
    }
}
