using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Assets.Script.Types;
using YARG.Song.Chart;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Player
{
    public enum HitStatus
    {
        Idle,
        Partial,
        Hit,
        Sustained,
        Dropped,
        Missed
    };

    public enum OverdriveStyle
    {
        RockBand,
        GuitarHero
    }

    public abstract class Player
    {
        public readonly struct InputNode
        {
            public readonly DualPosition position;
            public readonly object input;
            public InputNode(DualPosition position, object input)
            {
                this.position = position;
                this.input = input;
            }
        }

        protected static SyncTrack sync = new();
        protected static (FlatMapNode<DualPosition, BeatStyle>[], int) beats;
        protected static OverdriveStyle style;
        protected const long OVERDRIVE_MAX = (long) int.MaxValue + 1;
        protected const long OVERDRIVE_THRESHOLD = OVERDRIVE_MAX / 2;
        protected const long OVERDRIVE_PER_BEAT = OVERDRIVE_MAX / 32;
        protected const long OVERDRIVE_PER_MEASURE = OVERDRIVE_MAX / 8;

        public static void SetSync(SyncTrack sync, OverdriveStyle style)
        {
            Player.sync = sync;
            Player.beats = sync.beatMap.Data;
            Player.style = style;
        }

        protected readonly List<OverdrivePhrase> overdrives = new();
        protected int overdriveIndex = 0;
        protected bool overdriveActive = false;
        protected long overdrive = 0;
        protected long overdriveOffset = 0;
        protected long overdriveTime = 0;

        protected SemiQueue<(float, BeatStyle)> beatsOnScreen = new();
        protected int nextViewableBeat = 0;
        protected int currentBeatIndex_overdrive = 0;

        protected readonly PlayableSemiQueue notesOnScreen = new();
        protected (float, float) visibilityRange = new(1.0f, .3f);
        protected int nextViewable = 0;
        protected GameObject track;
        protected InputNode currentInput = new();

        protected int tempoIndex = 0;

        protected Player((GameObject, PlayerManager.Player) player)
        {
            track = player.Item1;
        }

        public abstract void EnqueueHittables(float position);
        public abstract void RunInput();
        public abstract void PostLoopCleanup(float position);
        public abstract void UpdateNotesOnScreen(float position);
        public abstract void Render(float position);

        public virtual void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is OverdrivePhrase overdrive)
                overdrives.Add(overdrive);
        }

        public void SetInput((float, object) input)
        {
            long ticks = sync.ConvertToTicks(input.Item1, ref tempoIndex);
            currentInput = new(new(ticks, input.Item1), input.Item2);
        }

        public void CompleteOverdrivePhrase()
        {
            AddOverdrive(OVERDRIVE_PER_MEASURE * 2);
        }

        public void AddOverdrive(long overdrive)
        {
            Debug.Log($"Adding overdrive - {(float) overdrive / OVERDRIVE_MAX}");
            overdriveOffset += overdrive;
        }

        public void RemoveOverdrive(long overdrive)
        {
            Debug.Log($"Removing overdrive - {(float) overdrive / OVERDRIVE_MAX}");
            overdriveOffset -= overdrive;
        }

        public void IncrementOverdrive()
        {
            Debug.Log($"Incrementing overdrive index - {overdriveIndex++}");
        }

        public void ActivateOverdrive(long tickPosition)
        {
            overdriveActive = true;
            overdriveTime = tickPosition;
            Debug.Log("Overdrive activated");

            if (style == OverdriveStyle.RockBand)
            {
                while (currentBeatIndex_overdrive < beats.Item2)
                {
                    int nextIndex = currentBeatIndex_overdrive + 1;
                    while (nextIndex < beats.Item2 && beats.Item1[nextIndex].obj == BeatStyle.WEAK)
                        nextIndex++;

                    if (nextIndex == beats.Item2 || overdriveTime < beats.Item1[nextIndex].key.ticks)
                        break;

                    currentBeatIndex_overdrive = nextIndex;
                }
            }
            else
            {
                while (currentBeatIndex_overdrive < beats.Item2)
                {
                    int nextIndex = currentBeatIndex_overdrive + 1;
                    while (nextIndex < beats.Item2 && beats.Item1[nextIndex].obj != BeatStyle.MEASURE)
                        nextIndex++;

                    if (nextIndex == beats.Item2 || overdriveTime < beats.Item1[nextIndex].key.ticks)
                        break;

                    currentBeatIndex_overdrive = nextIndex;
                }
            }
        }

        public void UpdateBeatsOnScreen(float position)
        {
            float startOfWindow = position + visibilityRange.Item1;
            float endOfWindow = position - visibilityRange.Item2;
            while (beatsOnScreen.Count > 0 && beatsOnScreen.Peek().Item1 < endOfWindow)
                beatsOnScreen.Dequeue();

            while (nextViewableBeat < beats.Item2)
            {
                ref var beat = ref beats.Item1[nextViewableBeat];
                if (beat.key.seconds >= startOfWindow)
                    break;

                if (beat.key.seconds >= endOfWindow)
                    beatsOnScreen.Enqueue(new(beat.key.seconds, beat.obj));
                ++nextViewableBeat;
            }
        }

        protected void ApplyOverdriveOffset()
        {
            overdrive += overdriveOffset;
            overdrive = Math.Clamp(overdrive, 0, OVERDRIVE_MAX);
            if (overdriveOffset != 0)
                Debug.Log($"Overdrive - {(float) overdrive / OVERDRIVE_MAX}");

            overdriveOffset = 0;
            if (overdriveActive && overdrive == 0)
            {
                Debug.Log("Overdrive disabled");
                overdriveActive = false;
            }
        }



        protected abstract bool HandleStatus(HitStatus status, PlayableNote hittable);
    }
}
