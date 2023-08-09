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
        internal static readonly byte[] SYNCTRACK =   Encoding.ASCII.GetBytes("[SyncTrack]");
        internal static readonly byte[] EVENTTRACK =  Encoding.ASCII.GetBytes("[Events]");
        internal static readonly EventCombo TEMPO =       new(Encoding.ASCII.GetBytes("B"),  ChartEvent.BPM );
        internal static readonly EventCombo TIMESIG =     new(Encoding.ASCII.GetBytes("TS"), ChartEvent.TIME_SIG );
        internal static readonly EventCombo ANCHOR =      new(Encoding.ASCII.GetBytes("A"),  ChartEvent.ANCHOR );
        internal static readonly EventCombo TEXT =        new(Encoding.ASCII.GetBytes("E"),  ChartEvent.EVENT );
        internal static readonly EventCombo SECTION =     new(Encoding.ASCII.GetBytes("SE"), ChartEvent.SECTION );
        internal static readonly EventCombo NOTE =        new(Encoding.ASCII.GetBytes("N"),  ChartEvent.NOTE );
        internal static readonly EventCombo SPECIAL =     new(Encoding.ASCII.GetBytes("S"),  ChartEvent.SPECIAL );
        internal static readonly EventCombo LYRIC =       new(Encoding.ASCII.GetBytes("L"),  ChartEvent.LYRIC );
        internal static readonly EventCombo VOCAL =       new(Encoding.ASCII.GetBytes("V"),  ChartEvent.VOCAL );
        internal static readonly EventCombo PERC =        new(Encoding.ASCII.GetBytes("VP"), ChartEvent.VOCAL_PERCUSSION );

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
            new(Encoding.ASCII.GetBytes("Keyboard]"),         NoteTracks_Chart.Keys ),
            new(Encoding.ASCII.GetBytes("GHLGuitar]"),    NoteTracks_Chart.GHLGuitar ),
            new(Encoding.ASCII.GetBytes("GHLBass]"),      NoteTracks_Chart.GHLBass ),
        };

        internal static readonly EventCombo[] EVENTS_SYNC   = { TEMPO, TIMESIG, ANCHOR };
        internal static readonly EventCombo[] EVENTS_EVENTS = { TEXT, SECTION, };
        internal static readonly EventCombo[] EVENTS_DIFF   = { NOTE, SPECIAL, TEXT, };

        static ChartFileReader() { }

        internal const double TEMPO_FACTOR = 60000000000;

        private readonly TxtFileReader reader;
        private readonly byte* ptr;
        private readonly int length;
        private bool disposeReader = false;

        private EventCombo[] eventSet = Array.Empty<EventCombo>();
        private long tickPosition = 0;
        public NoteTracks_Chart Instrument { get; private set; }
        public int Difficulty { get; private set; }

        public ChartFileReader(TxtFileReader reader, bool disposeReader = false)
        {
            this.reader = reader;
            this.ptr = reader.Ptr;
            this.length = reader.Length;
            this.disposeReader = disposeReader;
        }

        public ChartFileReader(FrameworkFile file) : this(new TxtFileReader(file.Ptr, file.Length), true) { }

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

        public bool ValidateSyncTrack()
        {
            if (!ValidateTrack(SYNCTRACK))
                return false;

            eventSet = EVENTS_SYNC;
            return true;
        }

        public bool ValidateEventsTrack()
        {
            if (!ValidateTrack(EVENTTRACK))
                return false;

            eventSet = EVENTS_EVENTS;
            return true;
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
            int position = reader.Position;
            if (position == length)
                return false;

            if (ptr[position] == '}')
            {
                reader.GotoNextLine();
                return false;
            }

            return true;
        }

        public (long, ChartEvent) ParseEvent()
        {
            static bool EqualSequences(byte* curr, int length, byte[] descriptor)
            {
                if (descriptor.Length != length) return false;
                for (int i = 0; i < length; ++i)
                    if (descriptor[i] != curr[i]) return false;
                return true;
            }

            long position = reader.ReadInt64();
            if (position < tickPosition)
                throw new Exception($".Cht/.Chart position out of order (previous: {tickPosition})");

            tickPosition = position;

            byte* ptr = reader.CurrentPtr;
            byte* start = ptr;
            while (true)
            {
                byte curr = (byte) (*ptr & ~32);
                if (curr < 'A' || 'Z' < curr)
                    break;
                ++ptr;
            }

            int length = (int)(ptr - start);
            reader.Position = (int)(ptr - this.ptr);
            foreach (var combo in eventSet)
                if (EqualSequences(start, length,combo.descriptor))
                {
                    reader.SkipWhiteSpace();
                    return new(position, combo.eventType);
                }
            return new(position, ChartEvent.UNKNOWN);
        }

        public void SkipEvent()
        {
            reader.GotoNextLine();
        }

        public void NextEvent()
        {
            reader.GotoNextLine();
        }

        public ReadOnlySpan<byte> ExtractTextAsBytes()
        {
            return reader.ExtractTextSpan();
        }

        public (int, long) ExtractLaneAndSustain()
        {
            int lane = reader.ReadInt32();
            long sustain = reader.ReadInt64();
            return new(lane, sustain);
        }

        public SpecialPhrase ExtractSpecialPhrase()
        {
            int type = reader.ReadInt32();
            long duration = reader.ReadInt64();
            return new((SpecialPhraseType)type, duration);
        }

        public int ExtractMicrosPerQuarter()
        {
            return (int)Math.Round(TEMPO_FACTOR / reader.ReadUInt32());
        }

        public long ExtractAnchor()
        {
            return reader.ReadInt64();
        }

        public TimeSig ExtractTimeSig()
        {
            ulong numerator = reader.ReadUInt64();
            ulong denom = 255, metro = 0, n32nds = 0;
            if (reader.ReadUInt64(ref denom))
                if (reader.ReadUInt64(ref metro))
                    reader.ReadUInt64(ref n32nds);

            return new TimeSig((byte)numerator, (byte)denom, (byte)metro, (byte)n32nds);
        }

        public void SkipTrack()
        {
            reader.GotoNextLine();
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

            reader.Position = length;
            reader.SetNextPointer();
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = length - position;
            byte* curr = ptr + position;
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
            while (IsStillCurrentTrack())
            {
                var name = reader.ExtractModifierName();
                if (validNodes.TryGetValue(name, out var node))
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
