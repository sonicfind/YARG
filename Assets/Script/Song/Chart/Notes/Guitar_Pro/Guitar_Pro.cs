using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace YARG.Song.Chart.Notes
{
    public interface IFretted
    {
        public uint Value { get; set; }
    }

    public struct Fret_17 : IFretted
    {
        const uint MAX_FRET = 17;
        private uint value;

        public uint Value
        {
            get { return value; }
            set
            {
                if (value > MAX_FRET)
                    throw new ArgumentOutOfRangeException(nameof(value));
                this.value = value;
            }
        }
    }

    public struct Fret_22 : IFretted
    {
        const uint MAX_FRET = 22;
        private uint value;

        public uint Value
        {
            get { return value; }
            set
            {
                if (value > MAX_FRET)
                    throw new ArgumentOutOfRangeException(nameof(value));
                this.value = value;
            }
        }
    }

    public enum StringMode
    {
        Normal,
        Bend,
        Muted,
        Tapped,
        Harmonics,
        Pinch_Harmonics
    };

    public enum ProSlide
    {
        None,
        Normal,
        Reversed
    };

    public enum EmphasisType
    {
        None,
        High,
        Middle,
        Low
    };

    public struct ProString<FretType> : IEnableable
        where FretType : unmanaged, IFretted
    {
        private TruncatableSustain _duration;
        public FretType fret;
        public StringMode mode;

        public ulong Duration
        {
            get { return _duration; }
            set { _duration = value; }
        }

        public bool IsActive()
        {
            return _duration.IsActive();
        }

        public void Disable()
        {
            _duration.Disable();
            fret.Value = 0;
            mode = StringMode.Normal;
        }
    }

    public class Guitar_Pro<FretType> : Note<ProString<FretType>>
        where FretType : unmanaged, IFretted
    {
        public ref ProString<FretType> this[int lane] => ref lanes[lane];

        public bool HOPO { get; set; }
        public bool ForceNumbering { get; set; }
        public ProSlide Slide { get; set; }
        public EmphasisType Emphasis { get; set; }

        public Guitar_Pro() : base(6) { }

        public ProSlide WheelSlide()
        {
            if (Slide == ProSlide.None)
                Slide = ProSlide.Normal;
            else if (Slide == ProSlide.Normal)
                Slide = ProSlide.Reversed;
            else
                Slide = ProSlide.None;
            return Slide;
        }

        public EmphasisType WheelEmphasis()
        {
            if (Emphasis == EmphasisType.None)
                Emphasis = EmphasisType.High;
            else if (Emphasis == EmphasisType.High)
                Emphasis = EmphasisType.Middle;
            else if (Emphasis == EmphasisType.Middle)
                Emphasis = EmphasisType.Low;
            else
                Emphasis = EmphasisType.None;
            return Emphasis;
        }

#nullable enable
        public override IPlayableNote ConvertToPlayable(in ulong position, in SyncTrack sync, in ulong prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }

    public unsafe struct Guitar_Pro_S<FretType> : INote_S
        where FretType : unmanaged, IFretted
    {
        public uint NumLanes => 6;
        private ProString<FretType> string_0;
        private ProString<FretType> string_1;
        private ProString<FretType> string_2;
        private ProString<FretType> string_3;
        private ProString<FretType> string_4;
        private ProString<FretType> string_5;
        public ref ProString<FretType> this[int lane]
        {
            get { fixed (ProString<FretType>* strings = &string_1) return ref strings[lane]; }
        }

        public bool HOPO { get; set; }
        public bool ForceNumbering { get; set; }
        public ProSlide Slide { get; set; }
        public EmphasisType Emphasis { get; set; }

        public ProSlide WheelSlide()
        {
            if (Slide == ProSlide.None)
                Slide = ProSlide.Normal;
            else if (Slide == ProSlide.Normal)
                Slide = ProSlide.Reversed;
            else
                Slide = ProSlide.None;
            return Slide;
        }

        public EmphasisType WheelEmphasis()
        {
            if (Emphasis == EmphasisType.None)
                Emphasis = EmphasisType.High;
            else if (Emphasis == EmphasisType.High)
                Emphasis = EmphasisType.Middle;
            else if (Emphasis == EmphasisType.Middle)
                Emphasis = EmphasisType.Low;
            else
                Emphasis = EmphasisType.None;
            return Emphasis;
        }

        public ulong GetLongestSustain()
        {
            ulong sustain = 0;
            fixed (ProString<FretType>* strings = &string_1)
            {
                for (int i = 0; i < 6; ++i)
                {
                    ulong end = strings[i].Duration;
                    if (end > sustain)
                        sustain = end;
                }
            }
            return sustain;
        }

        public bool HasActiveNotes()
        {
            fixed (ProString<FretType>* strings = &string_1)
                for (int i = 0; i < 6; ++i)
                    if (strings[i].IsActive())
                        return true;
            return false;
        }

        public IPlayableNote ConvertToPlayable<T>(in ulong position, in SyncTrack sync, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S
        {
            throw new NotImplementedException();
        }
    }
}
