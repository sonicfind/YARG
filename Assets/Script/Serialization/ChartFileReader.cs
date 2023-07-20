using YARG.Modifiers;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Serialization
{
    public enum ChartEvent
    {
        BPM,
        TIME_SIG,
        ANCHOR,
        EVENT,
        SECTION,
        NOTE,
        MULTI_NOTE,
        MODIFIER,
        SPECIAL,
        LYRIC,
        VOCAL,
        VOCAL_PERCUSSION,
        NOTE_PRO,
        MUTLI_NOTE_PRO,
        ROOT,
        LEFT_HAND,
        PITCH,
        RANGE_SHIFT,
        UNKNOWN = 255,
    }

    public enum NoteTracks_Chart
    {
        Single,
        DoubleGuitar,
        DoubleBass,
        DoubleRhythm,
        Drums,
        Keys,
        GHLGuitar,
        GHLBass,
        Invalid,
    };

    public unsafe class ChartFileReader : IDisposable
    {
        internal struct EventCombo
        {
            public byte[] descriptor;
            public ChartEvent eventType;
            public EventCombo(byte[] bytes, ChartEvent chartEvent)
            {
                descriptor = bytes;
                eventType = chartEvent;
            }
        }

        internal static readonly byte[] HEADERTRACK = Encoding.ASCII.GetBytes("[Song]");
        internal static readonly EventCombo TEXT =        new(Encoding.ASCII.GetBytes("E"),  ChartEvent.EVENT );
        internal static readonly EventCombo NOTE =        new(Encoding.ASCII.GetBytes("N"),  ChartEvent.NOTE );

        internal static readonly byte[][] DIFFICULTIES =
        {
            Encoding.ASCII.GetBytes("[Easy"),
            Encoding.ASCII.GetBytes("[Medium"),
            Encoding.ASCII.GetBytes("[Hard"),
            Encoding.ASCII.GetBytes("[Expert")
        };

        internal static readonly (byte[], NoteTracks_Chart)[] NOTETRACKS =
        {
            new(Encoding.ASCII.GetBytes("Single]"),       NoteTracks_Chart.Single ),
            new(Encoding.ASCII.GetBytes("DoubleGuitar]"), NoteTracks_Chart.DoubleGuitar ),
            new(Encoding.ASCII.GetBytes("DoubleBass]"),   NoteTracks_Chart.DoubleBass ),
            new(Encoding.ASCII.GetBytes("DoubleRhythm]"), NoteTracks_Chart.DoubleRhythm ),
            new(Encoding.ASCII.GetBytes("Drums]"),        NoteTracks_Chart.Drums ),
            new(Encoding.ASCII.GetBytes("Keys]"),         NoteTracks_Chart.Keys ),
            new(Encoding.ASCII.GetBytes("GHLGuitar]"),    NoteTracks_Chart.GHLGuitar ),
            new(Encoding.ASCII.GetBytes("GHLBass]"),      NoteTracks_Chart.GHLBass ),
        };

        internal static readonly EventCombo[] EVENTS_DIFF   = { NOTE, TEXT, };

        static ChartFileReader() { }

        private readonly TxtFileReader reader;
        private bool disposeReader = false;
        private EventCombo[] eventSet = Array.Empty<EventCombo>();
        private ulong tickPosition = 0;
        public NoteTracks_Chart Instrument { get; private set; }
        public int Difficulty { get; private set; }

        public ChartFileReader(TxtFileReader reader, bool disposeReader = false)
        {
            this.reader = reader;
            this.disposeReader = disposeReader;
        }

        public ChartFileReader(FrameworkFile file, bool disoseFile = false) : this(new TxtFileReader(file, disoseFile), true) { }

        public ChartFileReader(byte[] data) : this(new TxtFileReader(data), true) { }

        public ChartFileReader(string path) : this(new TxtFileReader(path), true) { }

        public ChartFileReader(PointerHandler handler, bool dispose = false) : this(new TxtFileReader(handler, dispose), true) { }

        public void Dispose()
        {
            if (disposeReader)
            {
                reader.Dispose();
                disposeReader = false;
            }
            GC.SuppressFinalize(this);
        }

        public bool IsStartOfTrack()
        {
            return !reader.IsEndOfFile() && reader.PeekByte() == '[';
        }

        public bool ValidateHeaderTrack()
        {
            return ValidateTrack(HEADERTRACK);
        }

        public bool ValidateDifficulty()
        {
            for (int diff = 3; diff >= 0; --diff)
                if (DoesStringMatch(DIFFICULTIES[diff]))
                {
                    Difficulty = diff;
                    eventSet = EVENTS_DIFF;
                    reader.Position += DIFFICULTIES[diff].Length;
                    return true;
                }
            return false;
        }

        public bool ValidateInstrument()
        {
            foreach (var track in NOTETRACKS)
            {
                if (ValidateTrack(track.Item1))
                {
                    Instrument = track.Item2;
                    return true;
                }
            }
            return false;
        }

        private bool ValidateTrack(ReadOnlySpan<byte> track)
        {
            if (!DoesStringMatch(track))
                return false;

            reader.GotoNextLine();
            tickPosition = 0;
            return true;
        }

        private bool DoesStringMatch(ReadOnlySpan<byte> str)
        {
            if (reader.Next - reader.Position < str.Length)
                return false;
            return reader.ExtractBasicSpan(str.Length).SequenceEqual(str);
        }

        public bool IsStillCurrentTrack()
        {
            if (reader.IsEndOfFile())
                return false;

            if (reader.PeekByte() == '}')
            {
                reader.GotoNextLine();
                return false;
            }

            return true;
        }

        public (ulong, ChartEvent) ParseEvent()
        {
            ulong position = reader.ReadUInt64();
            if (position < tickPosition)
                throw new Exception($".Cht/.Chart position out of order (previous: {tickPosition})");

            tickPosition = position;

            byte* ptr = reader.CurrentPtr;
            byte* start = ptr;
            while (('A' <= *ptr && *ptr <= 'Z') || ('a' <= *ptr && *ptr <= 'z'))
                ++ptr;

            var type = reader.ExtractBasicSpan((int)(ptr - start));
            reader.Position = (int)(ptr - reader.file.ptr);
            foreach (var combo in eventSet)
                if (type.SequenceEqual(combo.descriptor))
                {
                    reader.SkipWhiteSpace();
                    return new(position, combo.eventType);
                }
            return new(position, ChartEvent.UNKNOWN);
        }

        public void NextEvent()
        {
            reader.GotoNextLine();
        }

        public (uint, ulong) ExtractLaneAndSustain()
        {
            uint lane = reader.ReadUInt32();
            ulong sustain = reader.ReadUInt64();
            return new(lane, sustain);
        }

        public void SkipTrack()
        {
            reader.GotoNextLine();
            byte* ptr = reader.file.ptr;
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && ptr[point] <= 32 && ptr[point] != '\n')
                    --point;

                if (ptr[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    reader.GotoNextLine();
                    return;
                }

                position += next + 1;
            }

            reader.Position = reader.file.Length;
            reader.SetNextPointer();
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = reader.file.Length - position;
            byte* curr = reader.file.ptr + position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (curr[i] == '}')
                    return true;
                ++i;
            }
            return false;
        }

        public Dictionary<string, List<Modifier>> ExtractModifiers(Dictionary<string, ModifierNode> validNodes)
        {
            Dictionary<string, List<Modifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentTrack())
            {
                if (validNodes.TryGetValue(reader.ExtractModifierName(), out var node))
                {
                    var mod = node.CreateModifier(reader);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return modifiers;
        }
    }
}
