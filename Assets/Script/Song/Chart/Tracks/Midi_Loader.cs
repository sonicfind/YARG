using YARG.Serialization;
using YARG.Song.Chart;
using YARG.Song.Chart.Notes;
using YARG.Song.Chart.Vocals;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Song.Chart
{
    public struct Midi_Phrase
    {
        public readonly SpecialPhraseType type;
        public ulong position;
        public uint velocity;
        public Midi_Phrase(SpecialPhraseType type)
        {
            this.type = type;
            position = ulong.MaxValue;
            velocity = 0;
        }
    }

    public class Midi_PhraseList
    {
        private readonly (byte[], Midi_Phrase)[] _phrases;
        public Midi_PhraseList((byte[], Midi_Phrase)[] phrases) { _phrases = phrases; }

        public bool AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, MidiNote note)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                foreach (byte val in _phrases[i].Item1)
                {
                    if (val == note.value)
                    {
                        phrases.Get_Or_Add_Back(position);
                        _phrases[i].Item2.position = position;
                        _phrases[i].Item2.velocity = note.velocity;
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, MidiNote note)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                foreach (byte val in _phrases[i].Item1)
                {
                    if (val == note.value)
                    {
                        ref var phr = ref _phrases[i].Item2;
                        if (phr.position != ulong.MaxValue)
                        {
                            phrases.Traverse_Backwards_Until(phr.position).Add(new(phr.type, position - phr.position, phr.velocity));
                            phr.position = ulong.MaxValue;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AddPhrase(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, SpecialPhraseType type, byte velocity)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                ref var phr = ref _phrases[i].Item2;
                if (phr.type == type)
                {
                    phrases.Get_Or_Add_Back(position);
                    _phrases[i].Item2.position = position;
                    _phrases[i].Item2.velocity = velocity;
                    return true;
                }
            }
            return false;
        }

        public bool AddPhrase_Off(ref TimedFlatMap<List<SpecialPhrase>> phrases, ulong position, SpecialPhraseType type)
        {
            for (int i = 0; i < _phrases.Length; ++i)
            {
                ref var phr = ref _phrases[i].Item2;
                if (phr.type == type)
                {
                    if (phr.position != ulong.MaxValue)
                    {
                        phrases.Traverse_Backwards_Until(phr.position).Add(new(phr.type, position - phr.position, phr.velocity));
                        phr.position = ulong.MaxValue;
                    }
                    return true;
                }
            }
            return false;
        }
    }

    public class Midi_Loader
    {
        public static Encoding encoding = Encoding.UTF8;
    }
}
