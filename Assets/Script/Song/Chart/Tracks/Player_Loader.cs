using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Song.Chart
{
    public static class Player_Loader
    {
        public static Player[] SetupPlayers<T>(KeyValuePair<int, List<(GameObject track, PlayerManager.Player)>>[] playerMapping, SyncTrack sync, InstrumentTrack<T> track)
            where T : class, INote, new()
        {
            var result = new List<Player>();
            var phrases = !track.specialPhrases.IsEmpty() ? track.specialPhrases : null;
            foreach ((int diffIndex, var handlers) in playerMapping)
                result.AddRange(SetupPlayers(handlers.ToArray(), sync, track[diffIndex], phrases));
            return result.ToArray();
        }

        private static Player[] SetupPlayers<T>((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, DifficultyTrack<T> track, TimedFlatMap<List<SpecialPhrase>>? phrases = null)
            where T : class, INote, new()
        {
            var players = new Player_Instrument<T>[handlers.Length];

            (var notebuf, int count) = track.notes.Data;
            float[] notePositions = new float[count];

            int tempoIndex = 0;
            for (int i = 0; i < count; ++i)
                notePositions[i] = sync.ConvertToSeconds(notebuf[i].key, ref tempoIndex);

            for (int i = 0; i < handlers.Length; i++)
            {
                players[i] = new(handlers[i], (notebuf, count), notePositions);
            }

            int noteIndex = 0;
            tempoIndex = 0;
            foreach (FlatMapNode<long, List<SpecialPhrase>> node in phrases ?? track.specialPhrases)
            {
                while (noteIndex < count && notebuf[noteIndex].key < node.key)
                    ++noteIndex;

                int index = tempoIndex;
                foreach (var phrase in node.obj)
                {
                    index = tempoIndex;
                    long endTick = node.key + phrase.Duration;
                    int endIndex = noteIndex;
                    while (endIndex < count && notebuf[endIndex].key < endTick)
                        ++endIndex;

                    DualPosition position = new(node.key, sync.ConvertToSeconds(node.key, ref index));
                    DualPosition endPosition = new(endTick, sync.ConvertToSeconds(endTick, ref index));
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

                tempoIndex = index;
            }
            return players;
        }

        public static Player[] SetupDrumPlayers<T>(KeyValuePair<int, List<(GameObject track, PlayerManager.Player)>>[] playerMapping, SyncTrack sync, InstrumentTrack<T> track)
            where T : DrumNote, new()
        {
            var result = new List<Player>();
            var phrases = !track.specialPhrases.IsEmpty() ? track.specialPhrases : null;
            foreach ((int diffIndex, var handlers) in playerMapping)
                result.AddRange(SetupPlayers(handlers.ToArray(), sync, track[diffIndex], phrases));
            return result.ToArray();
        }

        private static Player[] SetupDrumPlayers<T>((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, DifficultyTrack<T> track, TimedFlatMap<List<SpecialPhrase>>? phrases = null)
            where T : DrumNote, new()
        {
            var players = new Player_Instrument<T>[handlers.Length];

            (var notebuf, int count) = track.notes.Data;
            float[] notePositions = new float[count];

            int tempoIndex = 0;
            for (int i = 0; i < count; ++i)
                notePositions[i] = sync.ConvertToSeconds(notebuf[i].key, ref tempoIndex);

            for (int i = 0; i < handlers.Length; i++)
            {
                players[i] = new(handlers[i], (notebuf, count), notePositions);
            }

            int noteIndex = 0;
            tempoIndex = 0;
            foreach (FlatMapNode<long, List<SpecialPhrase>> node in phrases ?? track.specialPhrases)
            {
                while (noteIndex < count && notebuf[noteIndex].key < node.key)
                    ++noteIndex;

                int index = tempoIndex;
                foreach (var phrase in node.obj)
                {
                    index = tempoIndex;
                    long endTick = node.key + phrase.Duration;
                    int endIndex = noteIndex;
                    while (endIndex < count && notebuf[endIndex].key < endTick)
                        ++endIndex;

                    DualPosition position = new(node.key, sync.ConvertToSeconds(node.key, ref index));
                    DualPosition endPosition = new(endTick, sync.ConvertToSeconds(endTick, ref index));
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

                tempoIndex = index;
            }
            return players;
        }
    }
}
