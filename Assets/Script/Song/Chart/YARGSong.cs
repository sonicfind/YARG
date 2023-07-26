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

namespace YARG.Song.Chart
{
    public readonly struct BeatPosition : IEquatable<BeatPosition>, IComparable<BeatPosition>
    {
        public readonly ulong ticks;
        public readonly float seconds;

        public BeatPosition(ulong ticks, float seconds)
        {
            this.ticks = ticks;
            this.seconds = seconds;
        }

        public int CompareTo(BeatPosition other)
        {
            return ticks.CompareTo(other.ticks);
        }

        public bool Equals(BeatPosition other)
        {
            return ticks.Equals(other.ticks);
        }
    }

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
        protected ulong m_hopo_frequency = 0;
        
        public string m_midiSequenceName = string.Empty;

        public ulong EndTick { get; private set; }
        public ulong LastNoteTick { get; private set; }

        public readonly SyncTrack                        m_sync = new();
        public readonly FlatMap<BeatPosition, BeatStyle> m_beatMap = new();
        public readonly SongEvents                       m_events = new();
        public readonly NoteTracks                       m_tracks = new();

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
                            if (!ParseBeats(reader))
                                Debug.Log("BEAT track appeared previously");
                        }
                        else if (!m_tracks.LoadFromMidi(type, m_baseDrumType, reader))
                            Debug.Log($"Track '{name}' already loaded");
                    }
                }
            }
            m_tracks.FinalizeProKeys();
        }

        protected void FinalizeData()
        {
            LastNoteTick = m_tracks.GetLastNoteTime();
            if (!m_events.globals.IsEmpty())
            {
                var node = m_events.globals.At_index(m_events.globals.Count - 1);
                if (node.obj.Last() == "[end]" && LastNoteTick < node.key)
                    EndTick = node.key;
            }

            m_sync.FinalizeTempoMap();
            if (m_beatMap.IsEmpty())
                GenerateAllBeats();
            else
                GenerateLeftoverBeats();
        }

        private void GenerateLeftoverBeats()
        {
            uint multipliedTickrate = 4u * m_sync.Tickrate;
            uint denominator = 0;
            int searchIndex = 0;
            for (int i = 0; i < m_sync.timeSigs.Count; ++i)
            {
                var node = m_sync.timeSigs.At_index(i);
                if (node.obj.Denominator != 255)
                    denominator = 1u << node.obj.Denominator;

                ulong ticksPerMarker = multipliedTickrate / denominator;
                ulong ticksPerMeasure = (multipliedTickrate * node.obj.Numerator) / denominator;

                ulong endTime;
                if (i + 1 < m_sync.timeSigs.Count)
                    endTime = m_sync.timeSigs.At_index(i + 1).key;
                else
                    endTime = LastNoteTick;

                while (node.key < endTime)
                {
                    ulong position = node.key;
                    for (uint n = 0; n < node.obj.Numerator && position < endTime; ++n, position += ticksPerMarker, ++searchIndex)
                    {
                        var beat = new BeatPosition(position, m_sync.ConvertToSeconds(position));
                        if (!m_beatMap.Contains(searchIndex, beat))
                            m_beatMap[beat] = BeatStyle.WEAK;
                    }
                    node.key += ticksPerMeasure;
                }
            }
        }

        private void GenerateAllBeats()
        {
            uint multipliedTickrate = 4u * m_sync.Tickrate;
            uint denominator = 0;
            uint metronome = 24;
            uint numerator = 4;
            for (int i = 0; i < m_sync.timeSigs.Count; ++i)
            {
                var node = m_sync.timeSigs.At_index(i);
                if (node.obj.Numerator > 0)
                    numerator = node.obj.Numerator;

                if (node.obj.Denominator != 255)
                    denominator = 1u << node.obj.Denominator;

                if (node.obj.Metronome != 0)
                    metronome = node.obj.Metronome;

                uint markersPerClick = 6 * denominator / metronome;
                ulong ticksPerMarker = multipliedTickrate / denominator;
                ulong ticksPerMeasure = (multipliedTickrate * numerator) / denominator;
                bool isIrregular = numerator > 4 || (numerator & 1) == 1;

                ulong endTime;
                if (i + 1 < m_sync.timeSigs.Count)
                    endTime = m_sync.timeSigs.At_index(i + 1).key;
                else
                    endTime = LastNoteTick;

                while (node.key < endTime)
                {
                    ulong position = node.key;
                    var style = BeatStyle.MEASURE;
                    int clickSpacing = (int)markersPerClick;
                    int triplSpacing = 3 * (int)markersPerClick;
                    for (int leftover = (int)numerator; leftover > 0 && position < endTime;)
                    {
                        int clicksLeft = clickSpacing;
                        do
                        {
                            var beat = new BeatPosition(position, m_sync.ConvertToSeconds(position));
                            m_beatMap.Add_NoReturn(beat, style);
                            position += ticksPerMarker;
                            style = BeatStyle.WEAK;
                            --clicksLeft;
                            --leftover;
                        } while (clicksLeft > 0 && leftover > 0 && position < endTime);
                        style = BeatStyle.STRONG;

                        if (isIrregular && leftover > 0 && position < endTime && markersPerClick < leftover && 2 * leftover < triplSpacing)
                        {
                            // leftover < 1.5 * spacing
                            clickSpacing = leftover;
                        }
                        
                    }
                    node.key += ticksPerMeasure;
                }
            }
        }

        private bool ParseBeats(MidiFileReader reader)
        {
            if (!m_beatMap.IsEmpty())
                return false;

            MidiNote note = new();
            while (reader.TryParseEvent())
            {
                var midiEvent = reader.GetEvent();
                if (midiEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    var beat = new BeatPosition(midiEvent.position, m_sync.ConvertToSeconds(midiEvent.position));
                    m_beatMap.Get_Or_Add_Back(beat) = note.value == 13 ? BeatStyle.MEASURE : BeatStyle.STRONG;
                }
            }
            return true;
        }
    }
}
