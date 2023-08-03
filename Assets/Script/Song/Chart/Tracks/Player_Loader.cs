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
        public static Player.Player[] Setup<T>(KeyValuePair<int, List<(GameObject track, PlayerManager.Player)>>[] playerMapping, SyncTrack sync, InstrumentTrack<T> track, long codaPosition)
            where T : class, INote, new()
        {
            var result = new List<Player.Player>();
            foreach ((int diffIndex, var handlers) in playerMapping)
                result.AddRange(Setup(handlers.ToArray(), sync, track[diffIndex], !track.specialPhrases.IsEmpty() ? track.specialPhrases : track[diffIndex].specialPhrases, codaPosition));
            return result.ToArray();
        }

        //public static Player[] SetupDrums<T>(KeyValuePair<int, List<(GameObject track, PlayerManager.Player)>>[] playerMapping, SyncTrack sync, InstrumentTrack<T> track, long codaPosition)
        //    where T : DrumNote, new()
        //{
        //    var result = new List<Player>();
        //    var phrases = !track.specialPhrases.IsEmpty() ? track.specialPhrases : null;
        //    foreach ((int diffIndex, var handlers) in playerMapping)
        //        result.AddRange(SetupDrums(handlers.ToArray(), sync, track[diffIndex], phrases, codaPosition));
        //    return result.ToArray();
        //}

        private static Player.Player[] Setup<T>((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, DifficultyTrack<T> track, TimedFlatMap<List<SpecialPhrase>> phrases, long codaPosition)
            where T : class, INote, new()
        {
            (var notebuf, int count) = track.notes.Data;
            float[] notePositions = new float[count];

            int tempoIndex = 0;
            for (int i = 0; i < count; ++i)
                notePositions[i] = sync.ConvertToSeconds(notebuf[i].key, ref tempoIndex);

            var players = new Player.Player_Sustained<T>[handlers.Length];
            for (int i = 0; i < handlers.Length; i++)
                players[i] = new(handlers[i], (notebuf, count), notePositions);

            int noteIndex = 0;
            tempoIndex = 0;
            foreach (FlatMapNode<long, List<SpecialPhrase>> node in phrases)
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
                                break;
                            case SpecialPhraseType.BRE:
                                if (node.key >= codaPosition)
                                    players[p].AttachPhrase(new BREPhrase(ref position, ref endPosition));
                                break;
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

        //private static Player[] SetupDrums<T>((GameObject track, PlayerManager.Player)[] handlers, SyncTrack sync, DifficultyTrack<T> track, TimedFlatMap<List<SpecialPhrase>>? phrases, long codaPosition)
        //    where T : DrumNote, new()
        //{
        //    (var notebuf, int count) = track.notes.Data;
        //    float[] notePositions = new float[count];

        //    int tempoIndex = 0;
        //    for (int i = 0; i < count; ++i)
        //        notePositions[i] = sync.ConvertToSeconds(notebuf[i].key, ref tempoIndex);

        //    var players = new Player_Drums<T>[handlers.Length];
        //    for (int i = 0; i < handlers.Length; i++)
        //        players[i] = new(handlers[i], (notebuf, count), notePositions);

        //    int noteIndex = 0;
        //    tempoIndex = 0;
        //    foreach (FlatMapNode<long, List<SpecialPhrase>> node in phrases)
        //    {
        //        while (noteIndex < count && notebuf[noteIndex].key < node.key)
        //            ++noteIndex;

        //        int index = tempoIndex;
        //        foreach (var phrase in node.obj)
        //        {
        //            index = tempoIndex;
        //            long endTick = node.key + phrase.Duration;
        //            int endIndex = noteIndex;
        //            while (endIndex < count && notebuf[endIndex].key < endTick)
        //                ++endIndex;

        //            DualPosition position = new(node.key, sync.ConvertToSeconds(node.key, ref index));
        //            DualPosition endPosition = new(endTick, sync.ConvertToSeconds(endTick, ref index));
        //            if (phrase.Type == SpecialPhraseType.BRE && node.key < codaPosition)
        //            {
        //                if (endIndex < count && notebuf[endIndex].key == endTick && notebuf[endIndex].obj is DrumNote drum)
        //                {
        //                    var pads = drum.pads;
        //                    for (int i = pads.Length - 1; i >= 0; --i)
        //                    {
        //                        if (pads[i].IsActive())
        //                        {
        //                            for (int p = 0; p < players.Length; ++p)
        //                                players[p].AttachPhrase(new OverdriveActivationPhrase(i, ref position, ref endPosition));
        //                            break;
        //                        }
        //                    }
        //                }
        //                continue;
        //            }

        //            for (int p = 0; p < players.Length; ++p)
        //            {
        //                var player = players[p];
        //                switch (phrase.Type)
        //                {
        //                    case SpecialPhraseType.StarPower:
        //                    case SpecialPhraseType.StarPower_Diff:
        //                        {
        //                            player.AttachPhrase(new OverdrivePhrase(player, endIndex - noteIndex, ref position, ref endPosition));
        //                            break;
        //                        }
        //                    case SpecialPhraseType.Solo:
        //                        {
        //                            player.AttachPhrase(new SoloPhrase(endIndex - noteIndex, ref position, ref endPosition));
        //                            break;
        //                        }
        //                    case SpecialPhraseType.FaceOff_Player1:
        //                    case SpecialPhraseType.FaceOff_Player2:
        //                        break;
        //                    case SpecialPhraseType.BRE:
        //                        if (node.key >= codaPosition)
        //                            players[p].AttachPhrase(new BREPhrase(ref position, ref endPosition));
        //                        break;
        //                    case SpecialPhraseType.Tremolo:
        //                    case SpecialPhraseType.Trill:
        //                        break;
        //                }
        //            }
        //        }

        //        tempoIndex = index;
        //    }
        //    return players;
        //}
    }
}
