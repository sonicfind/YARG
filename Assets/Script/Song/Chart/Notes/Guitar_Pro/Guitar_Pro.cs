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
        public int Value { get; set; }
    }

    public struct Fret_17 : IFretted
    {
        private const int MAX_FRET = 17;
        private int value;

        public int Value
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
        private const int MAX_FRET = 22;
        private int value;

        public int Value
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

        public long Duration
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
        public override int NumLanes => 6;
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
        public override PlayableNote ConvertToPlayable(in long position, in SyncTrack sync, in long prevPosition, in INote? prevNote)
        {
            throw new NotImplementedException();
        }
    }
}
