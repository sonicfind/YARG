
using YARG.Song.Chart.Notes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;
using YARG.Assets.Script.Types;

#nullable enable
namespace YARG.Song.Chart
{
    public interface IDifficultyTrack
    {
        public Player[] SetupPlayers(InputHandler[] handlers, SyncTrack sync, TimedFlatMap<List<SpecialPhrase>>? phrases = null);
    }

    public class DifficultyTrack<T> : Track, IDifficultyTrack
        where T : unmanaged, INote_S
    {
        public readonly TimedNativeFlatMap<T> notes = new();

        public override bool IsOccupied() { return !notes.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            notes.Clear();
        }
        public override void TrimExcess() => notes.TrimExcess();
        public override ulong GetLastNoteTime()
        {
            if (notes.IsEmpty()) return 0;

            var note = notes.At_index(notes.Count - 1);
            return note.key + note.obj.GetLongestSustain();
        }

        public unsafe Player[] SetupPlayers(InputHandler[] handlers, SyncTrack sync, TimedFlatMap<List<SpecialPhrase>>? phrases = null)
        {
            var players = new Player_Instrument_S<T>[handlers.Length];
            float[] notePositions = new float[notes.Count];
            for (int i = 0; i < notes.Count; ++i)
                notePositions[i] = sync.ConvertToSeconds(notes.At_index(i).key);

            for (int i = 0; i < handlers.Length; i++)
            {
                players[i] = new(handlers[i], notes, notePositions, sync);
            }

            int noteIndex = 0;
            foreach (FlatMapNode<ulong, List<SpecialPhrase>> node in phrases ?? specialPhrases)
            {
                while (noteIndex < notes.Count && notes.At_index(noteIndex).key < node.key)
                    ++noteIndex;

                foreach (var phrase in node.obj)
                {
                    ulong endTick = node.key + phrase.Duration;
                    int endIndex = noteIndex;
                    while (endIndex < notes.Count && notes.At_index(endIndex).key < endTick)
                        ++endIndex;

                    float position = sync.ConvertToSeconds(node.key);
                    float endPosition = sync.ConvertToSeconds(endTick);
                    for (int p = 0; p < players.Length; ++p)
                    {
                        var player = players[p];
                        switch (phrase.Type)
                        {
                            case SpecialPhraseType.StarPower:
                            case SpecialPhraseType.StarPower_Diff:
                                {
                                    player.AttachPhrase(position, endPosition, new OverdrivePhrase(player, endIndex - noteIndex));
                                    break;
                                }
                            case SpecialPhraseType.Solo:
                                {
                                    player.AttachPhrase(position, endPosition, new SoloPhrase<T>(player, endIndex - noteIndex));
                                    break;
                                }

                            case SpecialPhraseType.FaceOff_Player1:
                            case SpecialPhraseType.FaceOff_Player2:
                            case SpecialPhraseType.LyricLine:
                            case SpecialPhraseType.RangeShift:
                            case SpecialPhraseType.HarmonyLine:
                            case SpecialPhraseType.BRE:
                            case SpecialPhraseType.Tremolo:
                            case SpecialPhraseType.Trill:
                                break;
                        }
                    }
                    
                }
            }
            return players;
        }
    }
}
