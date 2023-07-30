using YARG.Serialization;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart
{
    public class SyncTrack
    {
        private uint tickrate;
        public readonly TimedFlatMap<Tempo> tempoMarkers = new();
        public readonly TimedFlatMap<TimeSig> timeSigs = new();
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

        internal const int MICROS_PER_SECOND = 1000000;

        public float ConvertToSeconds(long ticks)
        {
            var data = tempoMarkers.Data;
            int count = tempoMarkers.Count;
            for (int i = 0; i < count; i++)
            {
                ref var marker = ref data[i];
                if (i + 1 == count || ticks < data[i + 1].key)
                {
                    return ((marker.obj.Micros / (float)tickrate) * (ticks - marker.key) + marker.obj.Anchor) / MICROS_PER_SECOND;
                }
            }
            throw new Exception("dafuq");
        }
    };
}
