using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Serialization;
using YARG.Song.Chart;
using YARG.Song.Chart.Notes;
using YARG.Song.Entries;

namespace YARG.Song.Chart
{
    public class YARGConSong : YARGSong
    {
        private Encoding encoding;
        public YARGConSong(ConSongEntry entry) : base(entry) { encoding = entry.MidiEncoding; TruncatableSustain.MinDuration = 170; }

        public void Load_Midi(FrameworkFile file)
        {
            using MidiFileReader reader = new(file);
            m_sync.Tickrate = reader.GetTickRate();
            TruncatableSustain.MinDuration = (m_sync.Tickrate / 3);
            Parse(reader, encoding);
            FinalizeData();
        }

        public void Prepare_Midi(FrameworkFile file)
        {
            using MidiFileReader reader = new(file);
            TruncatableSustain.MinDuration = (ulong) (reader.GetTickRate() / 3);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() > 1 && reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events)
                    {
                        if (!m_tracks.LoadFromMidi(type, m_baseDrumType, reader))
                            Debug.Log($"Track '{name}' already loaded");
                    }
                }
            }
        }
    }
}
