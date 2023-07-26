
using YARG.Song.Chart.Notes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

#nullable enable
namespace YARG.Song.Chart
{
    public interface IDifficultyTrack
    {
        public Player_Instrument[] SetupPlayers(InputHandler[] handlers, TimedFlatMap<List<SpecialPhrase>>? phrases = null);
    }

    public class DifficultyTrack<T> : Track, IDifficultyTrack
        where T : unmanaged, INote_S
    {
        public readonly TimedFlatMap<T> notes = new();

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

        public unsafe Player_Instrument[] SetupPlayers(InputHandler[] handlers, TimedFlatMap<List<SpecialPhrase>>? phrases = null)
        {
            var players = new Player_Instrument[handlers.Length];
            var separateTracks = new (ulong, IPlayableNote)[handlers.Length][];
            for (int i = 0; i < separateTracks.Length; i++)
            {
                separateTracks[i] = new (ulong, IPlayableNote)[notes.Count];
                players[i] = new(separateTracks[i], handlers[i]);
            }

            var playableNotes = separateTracks[0];
            ulong prevPos = 0;
            T? prevNote = null;
            (ulong, IPlayableNote) playableNode;
            for (int i = 0; i < notes.Count; ++i)
            {
                var node = notes.At_index(i);
                playableNode.Item1 = node.key;
                playableNode.Item2 = node.obj.ConvertToPlayable(node.key, prevPos, prevNote);
                for (int j = 0; j < separateTracks.Length; ++j)
                    separateTracks[j][i] = playableNode;
            }
            
            int noteIndex = 0;
            foreach (FlatMapNode<ulong, List<SpecialPhrase>> node in phrases ?? specialPhrases)
            {
                while (noteIndex < playableNotes.Length && playableNotes[noteIndex].Item1 < node.key)
                    ++noteIndex;

                foreach (var phrase in node.obj)
                {
                    ulong endTick = node.key + phrase.Duration;
                    int endIndex = noteIndex;
                    while (endIndex < playableNotes.Length && playableNotes[endIndex].Item1 < endTick)
                        ++endIndex;

                    for (int p = 0; p < players.Length; ++p)
                    {
                        var player = players[p];
                        var track = separateTracks[p];
                        switch (phrase.Type)
                        {
                            case SpecialPhraseType.StarPower:
                            case SpecialPhraseType.StarPower_Diff:
                                {
                                    var overdrive = new OverdrivePhrase(player, endIndex - noteIndex);
                                    for (int i = noteIndex; i < endIndex; ++i)
                                        track[i].Item2.AttachPhrase(overdrive);
                                    player.AttachPhrase(node.key, endTick, overdrive);
                                    break;
                                }
                            case SpecialPhraseType.Solo:
                                {
                                    var solo = new SoloPhrase(player, endIndex - noteIndex);
                                    for (int i = noteIndex; i < endIndex; ++i)
                                        track[i].Item2.AttachPhrase(solo);
                                    player.AttachPhrase(node.key, endTick, solo);
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
