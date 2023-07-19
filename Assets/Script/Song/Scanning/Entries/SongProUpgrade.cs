using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Assets.Script.Types;

namespace YARG.Song.Entries
{
    public class SongProUpgrade
    {
        public AbridgedFileInfo Midi { get;  private set; }
#nullable enable
        private readonly CONFile? conFile;
        public FileListing? UpgradeMidiListing { get; private set; }

        public SongProUpgrade(CONFile? conFile, FileListing? listing, DateTime lastWrite)
        {
            this.conFile = conFile;
            UpgradeMidiListing = listing;
            Midi = new(UpgradeMidiListing != null ? UpgradeMidiListing.Filename : string.Empty, lastWrite);
        }

        public SongProUpgrade(string filename, DateTime lastWrite)
        {
            Midi = new(filename, lastWrite);
        }

        public void WriteToCache(BinaryWriter writer)
        {
            writer.Write(Midi.LastWriteTime.ToBinary());
        }

        public bool Validate()
        {
            if (Midi.FullName == string.Empty)
                return false;

            if (conFile != null)
                return UpgradeMidiListing != null && UpgradeMidiListing.lastWrite == Midi.LastWriteTime;
            return Midi.IsStillValid();
        }

        public FrameworkFile? LoadUpgradeMidi()
        {
            if (Midi.FullName == string.Empty)
                return null;

            if (conFile != null)
            {
                if (UpgradeMidiListing == null || UpgradeMidiListing.lastWrite != Midi.LastWriteTime)
                    return null;
                return new FrameworkFile_Pointer(conFile.LoadSubFile(UpgradeMidiListing)!, true);
            }

            if (!Midi.IsStillValid())
                return null;
            return new FrameworkFile_Alloc(Midi.FullName);
        }
    }
}
