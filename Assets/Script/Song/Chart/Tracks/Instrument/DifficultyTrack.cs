
using YARG.Song.Chart.Notes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;
using YARG.Assets.Script.Types;
using UnityEngine;

#nullable enable
namespace YARG.Song.Chart
{
    public interface IDifficultyTrack
    {
        public Player[] SetupPlayers((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, TimedFlatMap<List<SpecialPhrase>>? phrases = null);
    }

    public class DifficultyTrack<T> : Track, IDifficultyTrack
        where T : class, INote, new()
    {
        public readonly TimedFlatMap<T> notes = new();

        public override bool IsOccupied() { return !notes.IsEmpty() || base.IsOccupied(); }

        public override void Clear()
        {
            base.Clear();
            notes.Clear();
        }
        public override void TrimExcess() => notes.TrimExcess();
        public override long GetLastNoteTime()
        {
            if (notes.IsEmpty()) return 0;

            var note = notes.At_index(notes.Count - 1);
            return note.key + note.obj.GetLongestSustain();
        }

        public unsafe Player[] SetupPlayers((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, TimedFlatMap<List<SpecialPhrase>>? phrases = null)
        {
            var players = new Player_Instrument<T>[handlers.Length];
            int count = notes.Count;
            float[] notePositions = new float[count];

            var notebuf = notes.Data;
            for (int i = 0; i < count; ++i)
                notePositions[i] = sync.ConvertToSeconds(notebuf[i].key);

            for (int i = 0; i < handlers.Length; i++)
            {
                players[i] = new(handlers[i], notes, notePositions, sync);
            }

            int noteIndex = 0;
            foreach (FlatMapNode<long, List<SpecialPhrase>> node in phrases ?? specialPhrases)
            {
                while (noteIndex < count && notebuf[noteIndex].key < node.key)
                    ++noteIndex;

                foreach (var phrase in node.obj)
                {
                    long endTick = node.key + phrase.Duration;
                    int endIndex = noteIndex;
                    while (endIndex < count && notebuf[endIndex].key < endTick)
                        ++endIndex;

                    DualPosition position = new(node.key, sync);
                    DualPosition endPosition = new(endTick, sync);
                    for (int p = 0; p < players.Length; ++p)
                    {
                        var player = players[p];
                        switch (phrase.Type)
                        {
                            case SpecialPhraseType.StarPower:
                            case SpecialPhraseType.StarPower_Diff:
                                {
                                    player.AttachPhrase(new OverdrivePhrase(player, endIndex - noteIndex, ref position, ref endPosition));
                                    break;
                                }
                            case SpecialPhraseType.Solo:
                                {
                                    player.AttachPhrase(new SoloPhrase(endIndex - noteIndex, ref position, ref endPosition));
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
