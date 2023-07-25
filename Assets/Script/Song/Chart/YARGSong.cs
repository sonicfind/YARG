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
        protected ulong m_hopo_frequency = 0;

        public ushort Tickrate { get; protected set; }
        public string m_midiSequenceName = string.Empty;

        public ulong EndTick { get; private set; }
        public ulong LastNoteTick { get; private set; }

        public readonly SyncTrack                m_sync = new();
        public readonly TimedFlatMap<BeatStyle> m_beatMap = new();
        public readonly SongEvents               m_events = new();
        public readonly NoteTracks               m_tracks = new();

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

            m_sync.CheckStartOfTempoMap();
            if (m_beatMap.IsEmpty())
                GenerateAllBeats();
            else
                GenerateLeftoverBeats();
        }

        private void GenerateLeftoverBeats()
        {
            uint multipliedTickrate = 4u * Tickrate;
            uint denominator = 0;
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
                    for (uint n = 0; n < node.obj.Numerator && position < endTime; ++n, position += ticksPerMarker)
                        if (!m_beatMap.Contains(position))
                            m_beatMap[position] = BeatStyle.WEAK;
                    node.key += ticksPerMeasure;
                }
            }
        }

        private void GenerateAllBeats()
        {
            uint multipliedTickrate = 4u * Tickrate;
            uint denominator = 0;
            uint metronome = 24;
            for (int i = 0; i < m_sync.timeSigs.Count; ++i)
            {
                var node = m_sync.timeSigs.At_index(i);
                if (node.obj.Denominator != 255)
                    denominator = 1u << node.obj.Denominator;

                if (node.obj.Metronome != 0)
                    metronome = node.obj.Metronome;

                uint markersPerClick = 6 * denominator / metronome;
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
                    var style = BeatStyle.MEASURE;
                    for (uint n = 0; n < node.obj.Numerator && position < endTime;)
                    {
                        uint m = 0;
                        do
                        {
                            m_beatMap.Add_NoReturn(position, style);
                            position += ticksPerMarker;
                            style = BeatStyle.WEAK;
                            ++m;
                            ++n;
                        } while (m < markersPerClick && position < endTime);
                        style = BeatStyle.STRONG;

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
                    m_beatMap.Get_Or_Add_Back(midiEvent.position) = note.value == 13 ? BeatStyle.MEASURE : BeatStyle.STRONG;
                }
            }
            return true;
        }
    }
}
