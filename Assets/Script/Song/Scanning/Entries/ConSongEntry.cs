using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using YARG.Song;
using YARG.Audio;
using System.Buffers.Binary;
using UnityEngine;
using YARG.Song.Library;
using YARG.Data;
using YARG.Serialization.Parser;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using YARG.Assets.Script.Types;
using Cysharp.Threading.Tasks;

namespace YARG.Song.Entries
{
    public enum ConScanResult
    {
        Success,
        MissingMogg,
        UnsupportedEncryption,
        MissingMidi,
        PossibleCorruption
    }

    public class CONDifficulties
    {
        public short band = -1;
        public short lead_5 = -1;
        public short bass_5 = -1;
        public short rhythm = -1;
        public short coop = -1;
        public short keys = -1;
        public short drums_4 = -1;
        public short drums_4pro = -1;
        public short drums_5 = -1;
        public short proguitar = -1;
        public short probass = -1;
        public short proKeys = -1;
        public short leadVocals = -1;
        public short harmonyVocals = -1;

        public CONDifficulties() { }
        public CONDifficulties(BinaryFileReader reader)
        {
            unsafe
            {
                fixed (short* diffs = &band)
                    reader.CopyTo((byte*)diffs, 14 * sizeof(short));
            }
        }

        public void WriteToCache(BinaryWriter writer)
        {
            unsafe
            {
                fixed (short* diffs = &band)
                {
                    byte* yay = (byte*)diffs;
                    writer.Write(new ReadOnlySpan<byte>(yay, 14 * sizeof(short)));
                }
            }
        }
    }
#nullable enable
    public class ConSongEntry : SongEntry
    {
        internal static readonly float[,] emptyRatios = new float[0, 0];
        internal static readonly Encoding Latin1 = Encoding.GetEncoding("ISO-8859-1");
        static ConSongEntry() { }

        private CONFile? conFile;
        private readonly FileListing? midiListing;
        private FileListing? moggListing;
        private FileListing? miloListing;
        private FileListing? imgListing;

        public AbridgedFileInfo? DTA { get; private set; }

        private readonly CONDifficulties difficulties = new();

        public string SongID { get; private set; } = string.Empty;
        public uint AnimTempo { get; private set; }
        public string DrumBank { get; private set; } = string.Empty;
        public string VocalPercussionBank { get; private set; } = string.Empty;
        public uint VocalSongScrollSpeed { get; private set; }
        public uint SongRating { get; private set; } // 1 = FF; 2 = SR; 3 = M; 4 = NR
        public bool VocalGender { get; private set; } = true;//true for male, false for female
        public bool HasAlbumArt { get; private set; }
        public bool IsFake { get; private set; }
        public uint VocalTonicNote { get; private set; }
        public bool SongTonality { get; private set; } // 0 = major, 1 = minor
        public int TuningOffsetCents { get; private set; }

        public Encoding MidiEncoding { get; private set; } = Latin1;

        private string midiPath = string.Empty;
        public AbridgedFileInfo Midi { get; private set; }

        public AbridgedFileInfo? UpdateMidi { get; private set; } = null;

        public AbridgedFileInfo? Mogg { get; private set; } = null;
        public AbridgedFileInfo? Yarg_Mogg { get; private set; } = null;

        public AbridgedFileInfo? Milo { get; private set; } = null;
        public uint VenueVersion { get; private set; }

        public AbridgedFileInfo? Image { get; private set; } = null;

        private string location = string.Empty;


        public SongProUpgrade? Upgrade { get; set; }

        public string[] Soloes { get; private set; } = Array.Empty<string>();
        public string[] VideoVenues { get; private set; } = Array.Empty<string>();

        public int[] RealGuitarTuning { get; private set; } = Array.Empty<int>();
        public int[] RealBassTuning { get; private set; } = Array.Empty<int>();

        public int[] DrumIndices { get; private set; } = Array.Empty<int>();
        public int[] BassIndices { get; private set; } = Array.Empty<int>();
        public int[] GuitarIndices { get; private set; } = Array.Empty<int>();
        public int[] KeysIndices { get; private set; } = Array.Empty<int>();
        public int[] VocalsIndices { get; private set; } = Array.Empty<int>();
        public int[] CrowdIndices { get; private set; } = Array.Empty<int>();
        public int[] TrackIndices { get; private set; } = Array.Empty<int>();

        public float[] TrackStemValues { get; private set; } = Array.Empty<float>();
        public float[] DrumStemValues { get; private set; } = Array.Empty<float>();
        public float[] BassStemValues { get; private set; } = Array.Empty<float>();
        public float[] GuitarStemValues { get; private set; } = Array.Empty<float>();
        public float[] KeysStemValues { get; private set; } = Array.Empty<float>();
        public float[] VocalsStemValues { get; private set; } = Array.Empty<float>();
        public float[] CrowdStemValues { get; private set; } = Array.Empty<float>();

        public ConSongEntry(CONFile file, string nodeName, FileListing? midi, DateTime midiLastWrite, FileListing? moggListing, AbridgedFileInfo? moggInfo, AbridgedFileInfo? updateInfo, BinaryFileReader reader, CategoryCacheStrings strings) : base(reader, strings)
        {
            conFile = file;
            midiListing = midi;
            Midi = new(midi != null ? midi.Filename : string.Empty, midiLastWrite);

            if (moggListing != null)
                this.moggListing = moggListing;
            else if (moggInfo != null)
                Mogg = moggInfo;

            if (updateInfo != null)
                UpdateMidi = updateInfo;

            if (midiListing != null && !midiListing.Filename.StartsWith($"songs/{nodeName}"))
                nodeName = conFile[midiListing.pathIndex].Filename.Split('/')[1];

            string genPAth = $"songs/{nodeName}/gen/{nodeName}";
            if (reader.ReadBoolean())
                miloListing = conFile[genPAth + ".milo_xbox"];
            else
            {
                string milopath = reader.ReadLEBString();
                if (milopath != string.Empty)
                {
                    FileInfo info = new(milopath);
                    if (info.Exists)
                        Milo = info;
                }
            }

            if (reader.ReadBoolean())
                imgListing = conFile[genPAth + "_keep.png_xbox"];
            else
            {
                string imgpath = reader.ReadLEBString();
                if (imgpath != string.Empty)
                {
                    FileInfo info = new(imgpath);
                    if (info.Exists)
                        Image = info;
                }
            }
            difficulties = new(reader);
            FinishCacheRead(reader);
        }

        public ConSongEntry(AbridgedFileInfo midi, AbridgedFileInfo dta, AbridgedFileInfo? yargmogg, AbridgedFileInfo? mogg, AbridgedFileInfo? updateInfo, BinaryFileReader reader, CategoryCacheStrings strings) : base(reader, strings)
        {
            DTA = dta;
            Midi = midi;

            if (yargmogg != null)
                Yarg_Mogg = yargmogg;
            else
                Mogg = mogg!;

            if (updateInfo != null)
                UpdateMidi = updateInfo;

            Milo = new(reader.ReadLEBString());
            Image = new(reader.ReadLEBString());
            difficulties = new(reader);
            FinishCacheRead(reader);
        }

        private void FinishCacheRead(BinaryFileReader reader)
        {
            Directory = reader.ReadLEBString();
            AnimTempo = reader.ReadUInt32();
            SongID = reader.ReadLEBString();
            VocalPercussionBank = reader.ReadLEBString();
            VocalSongScrollSpeed = reader.ReadUInt32();
            SongRating = reader.ReadUInt32();
            VocalGender = reader.ReadBoolean();
            VocalTonicNote = reader.ReadUInt32();
            SongTonality = reader.ReadBoolean();
            TuningOffsetCents = reader.ReadInt32();
            VenueVersion = reader.ReadUInt32();

            RealGuitarTuning =  ReadIntArray(reader);
            RealBassTuning = ReadIntArray(reader);

            DrumIndices = ReadIntArray(reader);
            BassIndices = ReadIntArray(reader);
            GuitarIndices = ReadIntArray(reader);
            KeysIndices = ReadIntArray(reader);
            VocalsIndices = ReadIntArray(reader);
            TrackIndices = ReadIntArray(reader);
            CrowdIndices = ReadIntArray(reader);

            DrumStemValues = ReadFloatArray(reader);
            BassStemValues = ReadFloatArray(reader);
            GuitarStemValues = ReadFloatArray(reader);
            KeysStemValues = ReadFloatArray(reader);
            VocalsStemValues = ReadFloatArray(reader);
            TrackStemValues = ReadFloatArray(reader);
            CrowdStemValues = ReadFloatArray(reader);
        }

        private static int[] ReadIntArray(BinaryFileReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<int>();

            int[] values = new int[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadInt32();
            return values;
        }

        private static float[] ReadFloatArray(BinaryFileReader reader)
        {
            int length = reader.ReadInt32();
            if (length == 0)
                return Array.Empty<float>();

            float[] values = new float[length];
            for (int i = 0; i < length; ++i)
                values[i] = reader.ReadFloat();
            return values;
        }

        public ConSongEntry(CONFile conFile, string nodeName, DTAFileReader reader)
        {
            this.conFile = conFile;
            SetFromDTA(nodeName, reader);

            if (midiPath == string.Empty)
                midiPath = location + ".mid";

            midiListing = conFile[midiPath];
            if (midiListing == null)
                throw new Exception($"Required midi file '{midiPath}' was not located");

            Midi = new(midiPath, midiListing.lastWrite);
            string midiDirectory = conFile[midiListing.pathIndex].Filename;

            moggListing = conFile[location + ".mogg"];

            if (!location.StartsWith($"songs/{nodeName}"))
                nodeName = midiDirectory.Split('/')[1];

            string genPAth = $"songs/{nodeName}/gen/{nodeName}";
            miloListing = conFile[genPAth + ".milo_xbox"];
            imgListing =  conFile[genPAth + "_keep.png_xbox"];

            if (m_playlist.Str == string.Empty)
                m_playlist = conFile.filename;

            Directory = Path.Combine(conFile.filename, midiDirectory);
        }

        public ConSongEntry(string folder, AbridgedFileInfo dta, string nodeName, DTAFileReader reader)
        {
            DTA = dta;
            SetFromDTA(nodeName, reader);
            
            string file = Path.Combine(folder, location);

            if (midiPath == string.Empty)
                midiPath = file + ".mid";

            FileInfo midiInfo = new(midiPath);
            if (!midiInfo.Exists)
                throw new Exception($"Required midi file '{midiPath}' was not located");
            Midi = midiInfo;

            FileInfo mogg = new(file + ".yarg_mogg");
            if (mogg.Exists)
                Yarg_Mogg = mogg;
            else
                Mogg = new(file + ".mogg");

            if (!location.StartsWith($"songs/{nodeName}"))
                nodeName = location.Split('/')[1];

            file = Path.Combine(folder, $"songs/{nodeName}/gen/{nodeName}");
            Milo = new(file + ".milo_xbox");
            Image = new(file + "_keep.png_xbox");

            if (m_playlist.Str == string.Empty)
                m_playlist = folder;

            Directory = Path.GetDirectoryName(midiPath)!;
        }

        public (bool, bool) SetFromDTA(string nodeName, DTAFileReader reader)
        {
            bool alternatePath = false;
            bool discUpdate = false;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                switch (name)
                {
                    case "name": m_name = reader.ExtractText(); break;
                    case "artist": m_artist = reader.ExtractText(); break;
                    case "master": IsMaster = reader.ReadBoolean(); break;
                    case "context": /*Context = reader.ReadUInt32();*/ break;
                    case "song": SongLoop(ref reader); break;
                    case "song_vocals": while (reader.StartNode()) reader.EndNode(); break;
                    case "song_scroll_speed": VocalSongScrollSpeed = reader.ReadUInt32(); break;
                    case "tuning_offset_cents": TuningOffsetCents = reader.ReadInt32(); break;
                    case "bank": VocalPercussionBank = reader.ExtractText(); break;
                    case "anim_tempo":
                        {
                            string val = reader.ExtractText();
                            AnimTempo = val switch
                            {
                                "kTempoSlow" => 16,
                                "kTempoMedium" => 32,
                                "kTempoFast" => 64,
                                _ => uint.Parse(val)
                            };
                            break;
                        }
                    case "preview":
                        m_previewStart = reader.ReadUInt32();
                        m_previewEnd = reader.ReadUInt32();
                        break;
                    case "rank": RankLoop(ref reader); break;
                    case "solo": Soloes = reader.ExtractList_String().ToArray(); break;
                    case "genre": m_genre = reader.ExtractText(); break;
                    case "decade": /*Decade = reader.ExtractText();*/ break;
                    case "vocal_gender": VocalGender = reader.ExtractText() == "male"; break;
                    case "format": /*Format = reader.ReadUInt32();*/ break;
                    case "version": VenueVersion = reader.ReadUInt32(); break;
                    case "fake": /*IsFake = reader.ExtractText();*/ break;
                    case "downloaded": /*Downloaded = reader.ExtractText();*/ break;
                    case "game_origin":
                        {
                            string str = reader.ExtractText();
                            if ((str == "ugc" || str == "ugc_plus"))
                            {
                                if (!nodeName.StartsWith("UGC_"))
                                    m_source = "customs";
                            }
                            else
                                m_source = str;

                            // if the source is any official RB game or its DLC, charter = Harmonix
                            if (SongSources.GetSource(str).Type == SongSources.SourceType.RB)
                            {
                                m_charter = "Harmonix";
                            }

                            // if the source is meant for usage in TBRB, it's a master track
                            // TODO: NEVER assume localized version contains "Beatles"
                            if (SongSources.SourceToGameName(str).Contains("Beatles")) IsMaster = true;
                            break;
                        }
                    case "song_id": SongID = reader.ExtractText(); break;
                    case "rating": SongRating = reader.ReadUInt32(); break;
                    case "short_version": /*ShortVersion = reader.ReadUInt32();*/ break;
                    case "album_art": HasAlbumArt = reader.ReadBoolean(); break;
                    case "year_released":
                    case "year_recorded": YearAsNumber = reader.ReadInt32(); break;
                    case "album_name": m_album = reader.ExtractText(); break;
                    case "album_track_number": m_album_track = reader.ReadUInt16(); break;
                    case "pack_name": m_playlist = reader.ExtractText(); break;
                    case "base_points": /*BasePoints = reader.ReadUInt32();*/ break;
                    case "band_fail_cue": /*BandFailCue = reader.ExtractText();*/ break;
                    case "drum_bank": DrumBank = reader.ExtractText(); break;
                    case "song_length": m_song_length = reader.ReadUInt32(); break;
                    case "sub_genre": /*Subgenre = reader.ExtractText();*/ break;
                    case "author": m_charter = reader.ExtractText(); break;
                    case "guide_pitch_volume": /*GuidePitchVolume = reader.ReadFloat();*/ break;
                    case "encoding":
                        MidiEncoding = reader.ExtractText() switch
                        {
                            "Latin1" => Latin1,
                            "UTF8" => Encoding.UTF8,
                            _ => MidiEncoding
                        };
                        break;
                    case "vocal_tonic_note": VocalTonicNote = reader.ReadUInt32(); break;
                    case "song_tonality": SongTonality = reader.ReadBoolean(); break;
                    case "alternate_path": alternatePath = reader.ReadBoolean(); break;
                    case "real_guitar_tuning":
                        {
                            if (reader.StartNode())
                            {
                                RealGuitarTuning = reader.ExtractList_Int().ToArray();
                                reader.EndNode();
                            }
                            else
                                RealGuitarTuning = new[] { reader.ReadInt32() };
                            break;
                        }
                    case "real_bass_tuning":
                        {
                            if (reader.StartNode())
                            {
                                RealBassTuning = reader.ExtractList_Int().ToArray();
                                reader.EndNode();
                            }
                            else
                                RealBassTuning = new[] { reader.ReadInt32() };
                            break;
                        }
                    case "video_venues":
                        {
                            if (reader.StartNode())
                            {
                                VideoVenues = reader.ExtractList_String().ToArray();
                                reader.EndNode();
                            }
                            else
                                VideoVenues = new[] { reader.ExtractText() };
                            break;
                        }
                    case "extra_authoring":
                        foreach (string str in reader.ExtractList_String())
                        {
                            if (str == "disc_update")
                            {
                                discUpdate = true;
                                break;
                            }
                        }
                        break;
                }
                reader.EndNode();
            }

            return new(discUpdate, alternatePath);
        }

        private void SongLoop(ref DTAFileReader reader)
        {
            float[]? pan = null;
            float[]? volume = null;
            float[]? core = null;
            while (reader.StartNode())
            {
                string descriptor = reader.GetNameOfNode();
                switch (descriptor)
                {
                    case "name": location = reader.ExtractText(); break;
                    case "tracks": TracksLoop(ref reader); break;
                    case "crowd_channels": CrowdIndices = reader.ExtractList_Int().ToArray(); break;
                    case "vocal_parts": VocalParts = reader.ReadUInt16(); break;
                    case "pans":
                        if (reader.StartNode())
                        {
                            pan = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            pan = new[] { reader.ReadFloat() };
                        break;
                    case "vols":
                        if (reader.StartNode())
                        {
                            volume = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            volume = new[] { reader.ReadFloat() };
                        break;
                    case "cores":
                        if (reader.StartNode())
                        {
                            core = reader.ExtractList_Float().ToArray();
                            reader.EndNode();
                        }
                        else
                            core = new[] { reader.ReadFloat() };
                        break;
                    case "hopo_threshold": m_hopo_frequency = reader.ReadUInt32(); break;
                    case "midi_file": midiPath = reader.ExtractText(); break;
                }
                reader.EndNode();
            }

            if (pan != null && volume != null)
            {
                HashSet<int> pending = new();
                for (int i = 0; i < pan.Length; i++)
                    pending.Add(i);

                if (DrumIndices != Array.Empty<int>())
                    DrumStemValues = CalculateStemValues(DrumIndices, pan, volume, pending);

                if (BassIndices != Array.Empty<int>())
                    BassStemValues = CalculateStemValues(BassIndices, pan, volume, pending);

                if (GuitarIndices != Array.Empty<int>())
                    GuitarStemValues = CalculateStemValues(GuitarIndices, pan, volume, pending);

                if (KeysIndices != Array.Empty<int>())
                    KeysStemValues = CalculateStemValues(KeysIndices, pan, volume, pending);

                if (VocalsIndices != Array.Empty<int>())
                    VocalsStemValues = CalculateStemValues(VocalsIndices, pan, volume, pending);

                if (CrowdIndices != Array.Empty<int>())
                    CrowdStemValues = CalculateStemValues(CrowdIndices, pan, volume, pending);

                TrackIndices = pending.ToArray();
                TrackStemValues = CalculateStemValues(TrackIndices, pan, volume, pending);
            }
        }

        private void TracksLoop(ref DTAFileReader reader)
        {
            while (reader.StartNode())
            {
                while (reader.StartNode())
                {
                    switch (reader.GetNameOfNode())
                    {
                        case "drum":
                            {
                                if (reader.StartNode())
                                {
                                    DrumIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    DrumIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "bass":
                            {
                                if (reader.StartNode())
                                {
                                    BassIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    BassIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "guitar":
                            {
                                if (reader.StartNode())
                                {
                                    GuitarIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    GuitarIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "keys":
                            {
                                if (reader.StartNode())
                                {
                                    KeysIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    KeysIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                        case "vocals":
                            {
                                if (reader.StartNode())
                                {
                                    VocalsIndices = reader.ExtractList_Int().ToArray();
                                    reader.EndNode();
                                }
                                else
                                    VocalsIndices = new[] { reader.ReadInt32() };
                                break;
                            }
                    }
                    reader.EndNode();
                }
                reader.EndNode();
            }
        }

        private static readonly int[] BandDiffMap = { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap = { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap = { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap = { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap = { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap = { 132, 175, 218, 279, 353, 427 };

        private void RankLoop(ref DTAFileReader reader)
        {
            int diff = 0;
            while (reader.StartNode())
            {
                string name = reader.GetNameOfNode();
                diff = reader.ReadInt32();
                switch (name)
                {
                    case "drum":
                    case "drums":
                        difficulties.drums_4 = (short)diff;
                        SetRank(ref m_scans.drums_4.intensity, diff, DrumDiffMap);
                        if (m_scans.drums_4pro.intensity == -1)
                            m_scans.drums_4pro.intensity = m_scans.drums_4.intensity;
                        break;
                    case "guitar":
                        difficulties.lead_5 = (short)diff;
                        SetRank(ref m_scans.lead_5.intensity, diff, GuitarDiffMap);
                        break;
                    case "bass":
                        difficulties.bass_5 = (short) diff;
                        SetRank(ref m_scans.bass_5.intensity, diff, BassDiffMap);
                        break;
                    case "vocals":
                        difficulties.leadVocals = (short)diff;
                        SetRank(ref m_scans.leadVocals.intensity, diff, VocalsDiffMap);
                        break;
                    case "keys":
                        difficulties.keys = (short)diff;
                        SetRank(ref m_scans.keys.intensity, diff, KeysDiffMap);
                        break;
                    case "realGuitar":
                    case "real_guitar":
                        difficulties.proguitar = (short)diff;
                        SetRank(ref m_scans.proguitar_17.intensity, diff, RealGuitarDiffMap);
                        m_scans.proguitar_22.intensity = m_scans.proguitar_17.intensity;
                        break;
                    case "realBass":
                    case "real_bass":
                        difficulties.probass = (short)diff;
                        SetRank(ref m_scans.probass_17.intensity, diff, RealBassDiffMap);
                        m_scans.probass_22.intensity = m_scans.probass_17.intensity;
                        break;
                    case "realKeys":
                    case "real_keys":
                        difficulties.proKeys = (short)diff;
                        SetRank(ref m_scans.proKeys.intensity, diff, RealKeysDiffMap);
                        break;
                    case "realDrums":
                    case "real_drums":
                        difficulties.drums_5 = difficulties.drums_4pro = (short)diff;
                        SetRank(ref m_scans.drums_4pro.intensity, diff, RealDrumsDiffMap);
                        if (m_scans.drums_4.intensity == -1)
                            m_scans.drums_4.intensity = m_scans.drums_4pro.intensity;
                        break;
                    case "harmVocals":
                    case "vocal_harm":
                        difficulties.harmonyVocals = (short) diff;
                        SetRank(ref m_scans.harmonyVocals.intensity, diff, HarmonyDiffMap);
                        break;
                    case "band":
                        difficulties.band = (short) diff;
                        SetRank(ref m_bandIntensity, diff, BandDiffMap);
                        break;
                }
                reader.EndNode();
            }
        }

        private static void SetRank(ref sbyte intensity, int rank, int[] values)
        {
            sbyte i = 0;
            while (i < 6 && values[i] <= rank)
                ++i;
            intensity = i;
        }

        public unsafe ScanResult Scan(string nodeName)
        {
            if (m_name.Length == 0)
            {
                Debug.Log($"{nodeName} - Name of song not defined");
                return ScanResult.NoName;
            }

            if (Mogg == null && moggListing == null)
            {
                Debug.Log($"{nodeName} - Mogg not defined");
                return ScanResult.MissingMogg;
            }

            if (!IsMoggUnencrypted())
            {
                Debug.Log($"{nodeName} - Mogg encrypted");
                return ScanResult.UnsupportedEncryption;
            }

            try
            {
                using var chartFile = LoadMidiFile();
                using var updateFile = LoadMidiUpdateFile();
                using var upgradeFile = Upgrade?.LoadUpgradeMidi();

                int bufLength = 0;
                if (UpdateMidi != null)
                {
                    if (updateFile == null)
                        throw new Exception("Update midi file was changed mid-scan");

                    Scan_Midi(updateFile, DrumType.FOUR_PRO, true);
                    bufLength += updateFile.Length;
                }

                if (Upgrade != null)
                {
                    if (upgradeFile == null)
                        throw new Exception("Upgrade midi file was changed mid-scan");

                    Scan_Midi(upgradeFile, DrumType.FOUR_PRO, true);
                    bufLength += upgradeFile.Length;
                }

                if (chartFile == null)
                    throw new Exception("Main midi file was changed mid-scan");

                Scan_Midi(chartFile, DrumType.FOUR_PRO, true);

                if (!m_scans.CheckForValidScans())
                    return ScanResult.NoNotes;

                m_scans.drums_4.subTracks = m_scans.drums_4pro.subTracks;

                bufLength += chartFile.Length;

                SetVocalsCount();

                using PointerHandler buffer = new(bufLength);
                Copier.MemCpy(buffer.Data, chartFile.ptr, (nuint)chartFile.Length);
                chartFile.Dispose();

                int offset = chartFile.Length;
                if (updateFile != null)
                {
                    Copier.MemCpy(buffer.Data + offset, updateFile.ptr, (nuint)updateFile.Length);
                    updateFile.Dispose();
                    offset += updateFile!.Length;
                }

                if (upgradeFile != null)
                {
                    Copier.MemCpy(buffer.Data + offset, upgradeFile.ptr, (nuint)upgradeFile.Length);
                    upgradeFile.Dispose();
                    offset += upgradeFile.Length;
                }

                m_hash = buffer.CalcHash128();
                return ScanResult.Success;
            }
            catch
            {
                return ScanResult.PossibleCorruption;
            }
        }

        public void Update(string folder, string nodeName, DTAFileReader reader)
        {
            var results = SetFromDTA(nodeName, reader);

            string dir = Path.Combine(folder, nodeName);
            FileInfo info;
            if (results.Item1)
            {
                string path = Path.Combine(dir, $"{nodeName}_update.mid");
                info = new(path);
                if (info.Exists)
                {
                    if (UpdateMidi == null || UpdateMidi.LastWriteTime < info.LastWriteTime)
                        UpdateMidi = info;
                }
                else if (UpdateMidi == null)
                    Debug.Log($"Couldn't update song {nodeName} - update file {path} not found!");
            }

            info = new(Path.Combine(dir, $"{nodeName}_update.mogg"));
            if (info.Exists && (Mogg == null || Mogg.LastWriteTime < info.LastWriteTime))
                Mogg = info;

            dir = Path.Combine(dir, "gen");

            info = new(Path.Combine(dir, $"{nodeName}.milo_xbox"));
            if (info.Exists && (Milo == null || Milo.LastWriteTime < info.LastWriteTime))
                Milo = info;

            if (HasAlbumArt && results.Item2)
            {
                info = new(Path.Combine(dir, $"{nodeName}_keep.png_xbox"));
                if (info.Exists && (Image == null || Image.LastWriteTime < info.LastWriteTime))
                    Image = info;
            }
        }

        public byte[] FormatCacheData(CategoryCacheWriteNode node)
        {
            using MemoryStream ms = new();
            using BinaryWriter writer = new(ms);

            writer.Write(Midi.FullName);
            writer.Write(Midi.LastWriteTime.ToBinary());

            if (conFile != null)
            {
                if (Mogg == null)
                {
                    writer.Write(true);
                    writer.Write(moggListing!.Filename);
                    writer.Write(moggListing.lastWrite.ToBinary());
                }
                else
                {
                    writer.Write(false);
                    writer.Write(Mogg.FullName);
                    writer.Write(Mogg.LastWriteTime.ToBinary());
                }
            }
            else
            {
                if (Yarg_Mogg != null)
                {
                    writer.Write(true);
                    writer.Write(Yarg_Mogg.FullName);
                    writer.Write(Yarg_Mogg.LastWriteTime.ToBinary());
                }
                else
                {
                    writer.Write(false);
                    writer.Write(Mogg!.FullName);
                    writer.Write(Mogg.LastWriteTime.ToBinary());
                }
            }

            if (UpdateMidi != null)
            {
                writer.Write(true);
                writer.Write(UpdateMidi.FullName);
                writer.Write(UpdateMidi.LastWriteTime.ToBinary());
            }
            else
                writer.Write(false);

            FormatCacheData(writer, node);

            if (conFile != null)
            {
                writer.Write(Milo == null);
                if (Milo != null)
                    WriteFileInfo(Milo, writer);

                writer.Write(Image == null);
                if (Image != null)
                    WriteFileInfo(Image, writer);
            }
            else
            {
                WriteFileInfo(Milo, writer);
                WriteFileInfo(Image, writer);
            }

            difficulties.WriteToCache(writer);
            writer.Write(Directory);
            writer.Write(AnimTempo);
            writer.Write(SongID);
            writer.Write(VocalPercussionBank);
            writer.Write(VocalSongScrollSpeed);
            writer.Write(SongRating);
            writer.Write(VocalGender);
            writer.Write(VocalTonicNote);
            writer.Write(SongTonality);
            writer.Write(TuningOffsetCents);
            writer.Write(VenueVersion);

            WriteArray(RealGuitarTuning, writer);
            WriteArray(RealBassTuning, writer);

            WriteArray(DrumIndices, writer);
            WriteArray(BassIndices, writer);
            WriteArray(GuitarIndices, writer);
            WriteArray(KeysIndices, writer);
            WriteArray(VocalsIndices, writer);
            WriteArray(TrackIndices, writer);
            WriteArray(CrowdIndices, writer);

            WriteArray(DrumStemValues, writer);
            WriteArray(BassStemValues, writer);
            WriteArray(GuitarStemValues, writer);
            WriteArray(KeysStemValues, writer);
            WriteArray(VocalsStemValues, writer);
            WriteArray(TrackStemValues, writer);
            WriteArray(CrowdStemValues, writer);

            return ms.ToArray();
        }

        private static void WriteFileInfo(AbridgedFileInfo? info, BinaryWriter writer)
        {
            if (info != null)
                writer.Write(info.FullName);
            else
                writer.Write(string.Empty);
        }

        private static void WriteArray(int[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        private static void WriteArray(float[] values, BinaryWriter writer)
        {
            int length = values.Length;
            writer.Write(length);
            for (int i = 0; i < length; ++i)
                writer.Write(values[i]);
        }

        public FrameworkFile? LoadMidiFile()
        {
            if (conFile != null)
            {
                if (midiListing == null)
                    return null;
                return new FrameworkFile_Pointer(conFile.LoadSubFile(midiListing)!, true);
            }

            if (!Midi.IsStillValid())
                return null;
            return new FrameworkFile_Alloc(Midi.FullName);
        }

        public FrameworkFile_Alloc? LoadMidiUpdateFile()
        {
            if (UpdateMidi == null)
                return null;

            FileInfo info = new(UpdateMidi.FullName);
            if (!info.Exists || info.LastWriteTime != UpdateMidi.LastWriteTime)
                return null;
            return new(UpdateMidi.FullName);
        }

        public FrameworkFile? LoadMoggFile()
        {
            if (Yarg_Mogg != null)
            {
                // ReSharper disable once MustUseReturnValue
                return new FrameworkFile_Handle(YargMoggReadStream.DecryptMogg(Yarg_Mogg.FullName));
            }

            if (Mogg != null && File.Exists(Mogg.FullName))
                return new FrameworkFile_Alloc(Mogg.FullName);

            if (moggListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(moggListing)!, true);

            return null;
        }

        public FrameworkFile? LoadMiloFile()
        {
            if (Milo != null && File.Exists(Milo.FullName))
                return new FrameworkFile_Alloc(Milo.FullName);

            if (miloListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(miloListing)!, true);

            return null;
        }

        public FrameworkFile? LoadImgFile()
        {
            if (Image != null && File.Exists(Image.FullName))
                return new FrameworkFile_Alloc(Image.FullName);

            if (imgListing != null)
                return new FrameworkFile_Pointer(conFile!.LoadSubFile(imgListing)!, true);

            return null;
        }

        public bool IsMoggUnencrypted()
        {
            if (Yarg_Mogg != null)
            {
                if (!File.Exists(Yarg_Mogg.FullName))
                    throw new Exception("YARG Mogg file not present");
                return YargMoggReadStream.GetVersionNumber(Yarg_Mogg.FullName) == 0xF0;
            }
            else if (Mogg != null && File.Exists(Mogg.FullName))
            {
                using var fs = new FileStream(Mogg.FullName, FileMode.Open, FileAccess.Read);
                return fs.ReadInt32LE() == 0x0A;
            }
            else if (conFile != null)
                return conFile.GetMoggVersion(moggListing!) == 0x0A;

            throw new Exception("Mogg file not present");
        }

        public override bool IsBelow(SongEntry rhs, SongAttribute attribute)
        {
            if (attribute == SongAttribute.PLAYLIST && rhs is ConSongEntry other)
                return difficulties.band < other.difficulties.band;
            return base.IsBelow(rhs, attribute);
        }

        public override void LoadAudio(IAudioManager manager, float speed, params SongStem[] ignoreStems)
        {
            var file = LoadMoggFile();
            if (file == null)
                throw new Exception("Mogg file not present");

            unsafe
            {
                switch (BinaryPrimitives.ReadInt32LittleEndian(new(file.ptr, 4)))
                {
                    case 0x0A:
                    case 0xF0:
                        break;
                    default:
                        throw new Exception("Original supported mogg replaced by an unsupported mogg");
                }
            }

            List<(SongStem, int[], float[])> stemMaps = new();
            if (DrumIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        stemMaps.Add(new(SongStem.Drums, DrumIndices, DrumStemValues));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemMaps.Add(new(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, DrumIndices[1..3], DrumStemValues[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemMaps.Add(new(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, DrumIndices[1..2], DrumStemValues[2..4]));
                        stemMaps.Add(new(SongStem.Drums3, DrumIndices[2..4], DrumStemValues[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemMaps.Add(new(SongStem.Drums1, DrumIndices[0..1], DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, DrumIndices[1..3], DrumStemValues[2..6]));
                        stemMaps.Add(new(SongStem.Drums3, DrumIndices[3..5], DrumStemValues[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemMaps.Add(new(SongStem.Drums1, DrumIndices[0..2], DrumStemValues[0..4]));
                        stemMaps.Add(new(SongStem.Drums2, DrumIndices[2..4], DrumStemValues[4..8]));
                        stemMaps.Add(new(SongStem.Drums3, DrumIndices[4..6], DrumStemValues[8..12]));
                        break;
                }
            }
            if (BassIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Bass))
                stemMaps.Add(new(SongStem.Bass, BassIndices, BassStemValues));

            if (GuitarIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Guitar))
                stemMaps.Add(new(SongStem.Guitar, GuitarIndices, GuitarStemValues));

            if (KeysIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Keys))
                stemMaps.Add(new(SongStem.Keys, KeysIndices, KeysStemValues));

            if (VocalsIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Vocals))
                stemMaps.Add(new(SongStem.Vocals, VocalsIndices, VocalsStemValues));

            if (TrackIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Song))
                stemMaps.Add(new(SongStem.Song, TrackIndices, TrackStemValues));

            if (CrowdIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Crowd))
                stemMaps.Add(new(SongStem.Crowd, CrowdIndices, CrowdStemValues));

            manager.LoadMogg(file, stemMaps, speed);
        }

        public override async UniTask<bool> LoadPreviewAudio(IAudioManager manager, float speed)
        {
            await UniTask.RunOnThreadPool(() => LoadAudio(manager, speed, SongStem.Crowd));
            return false;
        }

        public override unsafe YargChart LoadChart_Original()
        {
            Debug.Log("Reading .mid file");
            var readSettings = MidiParser.Settings; // we need to modify these
            readSettings.TextEncoding = MidiEncoding;
            
            MidiFile? midi = null;

            if (UpdateMidi != null)
            {
                using var filebuffer = LoadMidiFile();
                if (filebuffer == null)
                    throw new Exception("Update File not present");

                using var stream = new UnmanagedMemoryStream(filebuffer.ptr, filebuffer.Length);
                midi = MidiFile.Read(stream, readSettings);
            }

            // also, if this RB song has a pro upgrade, merge it as well
            if (Upgrade != null)
            {
                using var filebuffer = Upgrade.LoadUpgradeMidi();
                if (filebuffer == null)
                    throw new Exception("Upgrade File not present");

                var stream = new UnmanagedMemoryStream(filebuffer.ptr, filebuffer.Length);
                var tmpMidi = MidiFile.Read(stream, readSettings);
                if (midi == null)
                {
                    midi = tmpMidi;
                }
                else
                {
                    HashSet<string> tracksInMidi = new();
                    foreach (var track in midi.GetTrackChunks())
                        if (track.Events.Count > 0 && track.Events[0] is SequenceTrackNameEvent trackName)
                            tracksInMidi.Add(trackName.Text);

                    foreach (var track in tmpMidi.GetTrackChunks())
                        if (track.Events.Count > 0 && track.Events[0] is SequenceTrackNameEvent trackName && !tracksInMidi.Contains(trackName.Text))
                            midi.Chunks.Add(track);
                }
            }

            if (midi == null)
            {
                using var filebuffer = LoadMidiFile();
                if (filebuffer == null)
                    throw new Exception("Midi File not present");

                var stream = new UnmanagedMemoryStream(filebuffer.ptr, filebuffer.Length);
                midi = MidiFile.Read(stream, readSettings);
            }
            else
            {
                using var filebuffer = LoadMidiFile();
                if (filebuffer == null)
                    throw new Exception("Midi File not present");

                var stream = new UnmanagedMemoryStream(filebuffer.ptr, filebuffer.Length);
                var tmpMidi = MidiFile.Read(stream, readSettings);

                HashSet<string> tracksInMidi = new();
                foreach (var track in midi.GetTrackChunks())
                    if (track.Events.Count > 0 && track.Events[0] is SequenceTrackNameEvent trackName)
                        tracksInMidi.Add(trackName.Text);

                foreach (var track in tmpMidi.GetTrackChunks())
                    if (track.Events.Count > 0 && track.Events[0] is SequenceTrackNameEvent trackName && !tracksInMidi.Contains(trackName.Text))
                        midi.Chunks.Add(track);
                midi.ReplaceTempoMap(tmpMidi.GetTempoMap());
            }

            // TODO: NEVER assume localized version contains "Beatles"
            if (!SongSources.SourceToGameName(m_source).Contains("Beatles"))
            {
                // skip beatles venues cuz they're built different
                var miloTracks = MiloParser.GetMidiFromMilo(LoadMiloFile(), midi.GetTempoMap());
                foreach (var track in miloTracks)
                {
                    midi.Chunks.Add(track);
                }
            }

            YargChart chart = new(null);
            chart.InitializeArrays();
            new MidiParser(this, midi).Parse(chart);
            return chart;
        }

        public override bool ValidateChartFile()
        {
            if (UpdateMidi != null && !UpdateMidi.IsStillValid())
                return false;

            if (Upgrade != null && !Upgrade.Validate())
                return false;

            if (midiListing == null)
                return conFile == null && Midi.IsStillValid() && DTA!.IsStillValid();
            return midiListing.lastWrite == Midi.LastWriteTime;
        }

        private float[] CalculateStemValues(int[] indices, float[] pan, float[] volume, HashSet<int> pending)
        {
            float[] values = new float[2 * indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                float theta = (pan[index] + 1) * ((float) Math.PI / 4);
                float volRatio = (float) Math.Pow(10, volume[index] / 20);
                values[2 * i] = volRatio * (float) Math.Cos(theta);
                values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                pending.Remove(index);
            }
            return values;
        }

        //public override YARGSong? LoadChart()
        //{
        //    YARGConSong song = new(this);
        //    FrameworkFile? file;
        //    if (UpdateMidi != null)
        //    {
        //        file = LoadMidiUpdateFile();
        //        if (file == null)
        //            return null;
        //        song.Prepare_Midi(file);
        //    }

        //    if (Upgrade != null)
        //    {
        //        file = Upgrade.LoadUpgradeMidi();
        //        if (file == null)
        //            return null;
        //        song.Prepare_Midi(file);
        //    }

        //    file = LoadMidiFile();
        //    if (file == null)
        //        return null;
        //    song.Load_Midi(file);
        //    return song;
        //}
    }
}
