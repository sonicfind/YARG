using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Assets.Script.Types;

namespace YARG.Song.Chart
{
    public class SyncTrack
    {
        private uint tickrate;
        public readonly TimedFlatMap<Tempo> tempoMarkers = new();
        public readonly TimedFlatMap<TimeSig> timeSigs = new();
        public readonly FlatMap<DualPosition, BeatStyle> beatMap = new();

        public SyncTrack() { }

        public uint Tickrate
        {
            get { return tickrate; }
            set { tickrate = value; }
        }

        public void AddFromMidi(MidiFileReader reader)
        {
            while (reader.TryParseEvent())
            {
                var midiEvent = reader.GetEvent();
                switch (midiEvent.type)
                {
                    case MidiEventType.Tempo:
                        tempoMarkers.Get_Or_Add_Back(midiEvent.position).Micros = reader.ExtractMicrosPerQuarter();
                        break;
                    case MidiEventType.Time_Sig:
                        timeSigs.Get_Or_Add_Back(midiEvent.position) = reader.ExtractTimeSig();
                        break;
                }
            }
            FinalizeTempoMap();
        }

        public bool ParseBeats(MidiFileReader reader)
        {
            if (!beatMap.IsEmpty())
                return false;

            MidiNote note = new();
            int tempoIndex = 0;
            while (reader.TryParseEvent())
            {
                var midiEvent = reader.GetEvent();
                if (midiEvent.type == MidiEventType.Note_On)
                {
                    reader.ExtractMidiNote(ref note);
                    if (note.velocity > 0)
                    {
                        var beat = new DualPosition(midiEvent.position, ConvertToSeconds(midiEvent.position, ref tempoIndex));
                        beatMap.Get_Or_Add_Back(beat) = note.value == 12 ? BeatStyle.MEASURE : BeatStyle.STRONG;
                    }
                }
            }
            return true;
        }

        public void AddFromDotChart(ChartFileReader reader)
        {
            while (reader.IsStillCurrentTrack())
            {
                var trackEvent = reader.ParseEvent();
                switch (trackEvent.Item2)
                {
                    case ChartEvent.BPM:
                        tempoMarkers.Get_Or_Add_Back(trackEvent.Item1).Micros = reader.ExtractMicrosPerQuarter();
                        break;
                    case ChartEvent.ANCHOR:
                        tempoMarkers.Get_Or_Add_Back(trackEvent.Item1).Anchor = reader.ExtractAnchor();
                        break;
                    case ChartEvent.TIME_SIG:
                        timeSigs.Get_Or_Add_Back(trackEvent.Item1) = reader.ExtractTimeSig();
                        break;
                }
                reader.NextEvent();
            }
        }

        public void FinalizeTempoMap()
        {
            if (tempoMarkers.IsEmpty() || tempoMarkers.At_index(0).key != 0)
                tempoMarkers.Insert(0, 0, new(Tempo.MICROS_AT_120BPM));

            if (timeSigs.IsEmpty() || timeSigs.At_index(0).key != 0)
                timeSigs.Insert(0, 0, new(4, 2, 24, 8));
            else
            {
                ref var timeSig = ref timeSigs[0];
                if (timeSig.Denominator == 255)
                    timeSig.Denominator = 2;
            }

            var prevNode = tempoMarkers.At_index(0);
            for (int i = 1; i < tempoMarkers.Count; i++)
            {
                ref var marker = ref tempoMarkers.At_index(i);
                if (marker.obj.Anchor == 0)
                    marker.obj.Anchor = (long)(((marker.key - prevNode.key) / (float)tickrate) * prevNode.obj.Micros) + prevNode.obj.Anchor;
                prevNode = marker;
            }
        }

        public void FinalizeBeats(long endTick)
        {
            if (beatMap.IsEmpty())
                GenerateAllBeats(endTick);
            else
                GenerateLeftoverBeats(endTick);
        }

        internal const int MICROS_PER_SECOND = 1000000;

        public float ConvertToSeconds(long ticks, int index)
        {
            (var data, int count) = tempoMarkers.Data;
            for (int i = index; i < count; i++)
            {
                ref var marker = ref data[i];
                if (i + 1 == count || ticks < data[i + 1].key)
                {
                    return ((marker.obj.Micros * (ticks - marker.key) / (float) tickrate) + marker.obj.Anchor) / MICROS_PER_SECOND;
                }
            }
            throw new Exception("dafuq");
        }

        public float ConvertToSeconds(long ticks, ref int index)
        {
            (var data, int count) = tempoMarkers.Data;
            for (int i = index; i < count; i++)
            {
                ref var marker = ref data[i];
                if (i + 1 == count || ticks < data[i + 1].key)
                {
                    index = i;
                    return ((marker.obj.Micros * (ticks - marker.key) / (float) tickrate) + marker.obj.Anchor) / MICROS_PER_SECOND;
                }
            }
            throw new Exception("dafuq");
        }

        public long ConvertToTicks(float seconds, ref int index)
        {
            (var data, int count) = tempoMarkers.Data;
            float micros = seconds * MICROS_PER_SECOND;
            for (int i = index; i < count; i++)
            {
                ref var marker = ref data[i];
                if (i + 1 == count || micros < data[i + 1].obj.Anchor)
                {
                    index = i;
                    return (long)((micros - marker.obj.Anchor) * tickrate / marker.obj.Micros) + marker.key;
                }
            }
            throw new Exception("dafuq");
        }

        private void GenerateLeftoverBeats(long endTick)
        {
            uint multipliedTickrate = 4u * Tickrate;
            uint denominator = 0;
            int searchIndex = 0;
            int tempoIndex = 0;

            var sigs = timeSigs.Data;
            for (int i = 0; i < sigs.Item2; ++i)
            {
                var node = sigs.Item1[i];
                if (node.obj.Denominator != 255)
                    denominator = 1u << node.obj.Denominator;

                long ticksPerMarker = multipliedTickrate / denominator;
                long ticksPerMeasure = (multipliedTickrate * node.obj.Numerator) / denominator;

                long endTime;
                if (i + 1 < sigs.Item2)
                    endTime = sigs.Item1[i + 1].key;
                else
                    endTime = endTick;

                while (node.key < endTime)
                {
                    long position = node.key;
                    for (uint n = 0; n < node.obj.Numerator && position < endTime; ++n, position += ticksPerMarker, ++searchIndex)
                    {
                        var beat = new DualPosition(position, ConvertToSeconds(position, ref tempoIndex));
                        if (!beatMap.Contains(searchIndex, beat))
                            beatMap[beat] = BeatStyle.WEAK;
                    }
                    node.key += ticksPerMeasure;
                }
            }
        }

        private void GenerateAllBeats(long endTick)
        {
            uint multipliedTickrate = 4u * Tickrate;
            int metronome = 24;
            int tempoIndex = 0;

            var sigs = timeSigs.Data;
            for (int i = 0; i < sigs.Item2; ++i)
            {
                var node = sigs.Item1[i];

                int numerator = node.obj.Numerator > 0 ? node.obj.Numerator : 4;
                int denominator = node.obj.Denominator != 255 ? 1 << node.obj.Denominator : 4;

                if (node.obj.Metronome != 0)
                    metronome = node.obj.Metronome;

                int markersPerClick = 6 * denominator / metronome;
                long ticksPerMarker = multipliedTickrate / denominator;
                long ticksPerMeasure = (multipliedTickrate * numerator) / denominator;
                bool isIrregular = numerator > 4 || (numerator & 1) == 1;

                long endTime;
                if (i + 1 < sigs.Item2)
                    endTime = sigs.Item1[i + 1].key;
                else
                    endTime = endTick;

                while (node.key < endTime)
                {
                    long position = node.key;
                    var style = BeatStyle.MEASURE;
                    int clickSpacing = markersPerClick;
                    int triplSpacing = 3 * markersPerClick;
                    for (int leftover = numerator; leftover > 0 && (position < endTime || i + 1 == sigs.Item2);)
                    {
                        int clicksLeft = clickSpacing;
                        do
                        {
                            var beat = new DualPosition(position, ConvertToSeconds(position, ref tempoIndex));
                            beatMap.Add_NoReturn(beat, style);
                            position += ticksPerMarker;
                            style = BeatStyle.WEAK;
                            --clicksLeft;
                            --leftover;
                        } while (clicksLeft > 0 && leftover > 0 && position < endTime);
                        style = BeatStyle.STRONG;

                        if (isIrregular && leftover > 0 && position < endTime && markersPerClick < leftover && 2 * leftover <= triplSpacing)
                        {
                            // leftover < 1.5 * spacing
                            clickSpacing = leftover;
                        }

                    }
                    node.key += ticksPerMeasure;
                }
            }
        }
    };
}
