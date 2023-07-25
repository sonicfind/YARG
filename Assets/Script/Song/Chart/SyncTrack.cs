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
        public readonly TimedNativeFlatMap<Tempo> tempoMarkers = new();
        public readonly TimedNativeFlatMap<TimeSig> timeSigs = new();
        public SyncTrack() {}

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

        public void CheckStartOfTempoMap()
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
        }
    };
}
