using YARG.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using YARG.Modifiers;
using YARG.Types;
using YARG.Ini;
using YARG.Song.Entries;
using UnityEngine;
using YARG.Chart;
using UnityEngine.Rendering.Universal;
using TagLib.Mpeg;
using UnityEngine.UIElements;
using YARG.Assets.Script.Types;
using YARG.Song.Chart.Notes;
using YARG.Data;
using UnityEditor.Experimental.GraphView;

namespace YARG.Song.Chart
{
    public abstract class YARGSong
    {
        protected string m_directory = string.Empty;
        protected string m_name = string.Empty;
        protected string m_artist = string.Empty;
        protected string m_album = string.Empty;
        protected string m_genre = string.Empty;
        protected string m_year = string.Empty;
        protected string m_charter = string.Empty;
        protected string m_playlist = string.Empty;
        protected DrumType m_baseDrumType = DrumType.UNKNOWN;
        protected long m_hopo_frequency = -1;
        
        public string m_midiSequenceName = string.Empty;

        public long EndTick { get; private set; }
        public long LastNoteTick { get; private set; }
        public long HopoFrequency => m_hopo_frequency;

        public readonly SyncTrack                        m_sync = new();
        public readonly SongEvents                       m_events = new();
        public readonly NoteTracks                       m_tracks = new();

        public FlatMap<DualPosition, BeatStyle> BeatMap => m_sync.beatMap;

        public YARGSong() { }
        public YARGSong(string directory)
        {
            m_directory = Path.GetFullPath(directory);
        }

        public YARGSong(SongEntry entry) : this(entry.Directory)
        {
            m_name = entry.Name;
            m_artist = entry.Artist;
            m_album = entry.Album;
            m_genre = entry.Genre;
            m_year = entry.Year;
            m_charter = entry.Charter;
            m_playlist = entry.Playlist;
            m_hopo_frequency = entry.HopoFrequency;
            m_baseDrumType = entry.GetDrumType();
        }

        public Player.Player[] SetupPlayers(Dictionary<Instrument, Dictionary<int, List<(GameObject, PlayerManager.Player)>>> instruemntMap)
        {
            List<Player.Player> players = new();
            foreach (var dict in instruemntMap)
            {
                switch (dict.Key)
                {
                    case Instrument.INVALID:
                        break;
                    case Instrument.GUITAR:
                        players.AddRange(Player_Loader.Setup(dict.Value.ToArray(), m_sync, m_tracks.lead_5, long.MaxValue));
                        break;
                    case Instrument.BASS:
                        players.AddRange(Player_Loader.Setup(dict.Value.ToArray(), m_sync, m_tracks.bass_5, long.MaxValue));
                        break;
                    case Instrument.DRUMS:
                        break;
                    case Instrument.KEYS:
                        break;
                    case Instrument.VOCALS:
                        break;
                    case Instrument.REAL_GUITAR:
                        break;
                    case Instrument.REAL_BASS:
                        break;
                    case Instrument.REAL_DRUMS:
                        break;
                    case Instrument.REAL_KEYS:
                        break;
                    case Instrument.HARMONY:
                        break;
                    case Instrument.GH_DRUMS:
                        break;
                    case Instrument.RHYTHM:
                        players.AddRange(Player_Loader.Setup(dict.Value.ToArray(), m_sync, m_tracks.rhythm, long.MaxValue));
                        break;
                    case Instrument.GUITAR_COOP:
                        players.AddRange(Player_Loader.Setup(dict.Value.ToArray(), m_sync, m_tracks.coop, long.MaxValue));
                        break;
                }
            }
            return players.ToArray();
        }

        protected void Parse(MidiFileReader reader, Encoding encoding)
        {
            Midi_Loader.encoding = encoding;
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1)
                {
                    if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                        m_midiSequenceName = encoding.GetString(reader.ExtractTextOrSysEx());
                    m_sync.AddFromMidi(reader);
                }
                else if (reader.GetEvent().type == MidiEventType.Text_TrackName)
                {
                    string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                    if (MidiFileReader.TRACKNAMES.TryGetValue(name, out var type))
                    {
                        if (type == MidiTrackType.Events)
                        {
                            if (!m_events.AddFromMidi(reader, encoding))
                                 Debug.Log("EVENTS track appeared previously");
                        }
                        else if (type == MidiTrackType.Beat)
                        {
                            if (!m_sync.ParseBeats(reader))
                                Debug.Log("BEAT track appeared previously");
                        }
                        else if (!m_tracks.LoadFromMidi(type, m_baseDrumType, reader))
                            Debug.Log($"Track '{name}' already loaded");
                    }
                }
            }
            m_tracks.FinalizeProKeys();
        }

        protected void FinalizeData(bool finalizeSync)
        {
            EndTick = LastNoteTick = m_tracks.GetLastNoteTime();
            var globals = m_events.globals.Data;
            for (int i = globals.Item2 - 1; i >= 0; --i)
            {
                foreach (var ev in globals.Item1[i].obj)
                {
                    if (ev == "[end]")
                    {
                        if (LastNoteTick < globals.Item1[i].key)
                            EndTick = globals.Item1[i].key;
                        goto SYNCFinalization;
                    }
                }
            }

            if (!m_events.globals.IsEmpty())
            {
                var node = m_events.globals.At_index(m_events.globals.Count - 1);
                if (LastNoteTick < node.key)
                    EndTick = node.key;
            }

        SYNCFinalization:
            if (finalizeSync)
                m_sync.FinalizeTempoMap();
            m_sync.FinalizeBeats(EndTick);
        }
    }
}
