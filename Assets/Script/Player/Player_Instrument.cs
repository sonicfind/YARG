using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Assets.Script.Types;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Player
{
    
    public abstract class Player_Instrument<T> : Player
        where T : class, INote, new()
    {
        private (FlatMapNode<long, T>[], int) notes;
        protected float[] notePositions;

        protected readonly PlayableSemiQueue hittableQueue = new();
        protected readonly (float, float) hitWindow = new(.065f, .065f);
        protected int nextHittable = 0;
        protected Queue<float> viewableOverrides = new();

        private List<SoloPhrase> soloes = new();
        private int soloIndex = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        protected int comboCount = 0;

        protected long positionTracker = 0;
        protected T? noteTracker = null;
        

        public Player_Instrument((GameObject, PlayerManager.Player) player, (FlatMapNode<long, T>[], int) notes, float[] notePositions) : base(player)
        {
            this.notes = notes;
            this.notePositions = notePositions;
        }

        public override void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is SoloPhrase solo)
                soloes.Add(solo);
            else
                base.AttachPhrase(phrase);
        }

        public override void EnqueueHittables(float position)
        {
            float startOfWindow = position + hitWindow.Item1;
            while (nextHittable < notes.Item2)
            {
                float notePosition = notePositions[nextHittable];
                if (notePosition >= startOfWindow)
                    break;

                ref var node = ref notes.Item1[nextHittable];
                int index = notesOnScreen.Find(notePosition);
                if (index >= 0)
                    hittableQueue.Enqueue(notesOnScreen.At(index));
                else
                    AddToQueue(hittableQueue, notePosition, ref node);
                ++nextHittable;
            }
        }

        public override void UpdateNotesOnScreen(float position)
        {
            float startOfWindow = position + visibilityRange.Item1;
            float endOfWindow = position - visibilityRange.Item2;
            while (notesOnScreen.Count > 0 && notesOnScreen.Peek().position.seconds < endOfWindow)
                notesOnScreen.Dequeue();

            while (nextViewable < notes.Item2)
            {
                float notePosition = notePositions[nextViewable];
                if (notePosition >= startOfWindow)
                    break;

                if (notePosition >= endOfWindow)
                {
                    ref var node = ref notes.Item1[nextViewable];
                    int index = hittableQueue.Find(notePosition);
                    if (index >= 0)
                        notesOnScreen.Enqueue(hittableQueue.At(index));
                    else if (viewableOverrides.Count == 0 || viewableOverrides.Peek() != notePosition)
                        AddToQueue(notesOnScreen, notePosition, ref node);
                    else
                        viewableOverrides.Dequeue();
                }
                ++nextViewable;
            }
        }

        public override void Render(float position)
        {

        }

        public void UpdateSoloBox()
        {
            soloBoxUpdated = true;
        }

        protected abstract bool CheckOverdriveActivity();

        protected void DrainOverdrive(long tickPosition)
        {
            if (!CheckOverdriveActivity())
                return;

            if (style == OverdriveStyle.RockBand)
            {
                while (currentBeatIndex_overdrive + 1 < beats.Item2)
                {
                    int nextIndex = currentBeatIndex_overdrive + 1;
                    while (beats.Item1[nextIndex].obj == BeatStyle.WEAK && nextIndex + 1 < beats.Item2)
                        nextIndex++;

                    long currBeat = beats.Item1[currentBeatIndex_overdrive].key.ticks;
                    long nextBeat = beats.Item1[nextIndex].key.ticks;
                    float overdrivePerTick = (float) OVERDRIVE_PER_BEAT / (nextBeat - currBeat);
                    long pivot = tickPosition < nextBeat ? tickPosition : nextBeat;
                    RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                    overdriveTime = pivot;

                    if (tickPosition < nextBeat || nextIndex + 1 == beats.Item2)
                        break;

                    currentBeatIndex_overdrive = nextIndex;
                }
            }
            else
            {
                while (currentBeatIndex_overdrive + 1 < beats.Item2)
                {
                    int nextIndex = currentBeatIndex_overdrive + 1;
                    while (beats.Item1[nextIndex].obj != BeatStyle.MEASURE && nextIndex + 1 < beats.Item2)
                        nextIndex++;

                    long currMeasure = beats.Item1[currentBeatIndex_overdrive].key.ticks;
                    long nextMeasure = beats.Item1[nextIndex].key.ticks;
                    float overdrivePerTick = (float) OVERDRIVE_PER_MEASURE / (nextMeasure - currMeasure);
                    long pivot = tickPosition < nextMeasure ? tickPosition : nextMeasure;
                    RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                    overdriveTime = pivot;

                    if (tickPosition < nextMeasure || nextIndex + 1 == beats.Item2)
                        break;

                    currentBeatIndex_overdrive = nextIndex;
                }
            }
        }

        protected void ProcessInput()
        {
            DequeueMissedHittables(currentInput.position.seconds);
            if (comboCount > 0)
            {
                if (hittableQueue.Count > 0)
                {
                    var hittable = hittableQueue.Peek();
                    var result = hittable.TryHit(currentInput.input, true);
                    if (HandleStatus(result, hittable))
                        hittableQueue.Dequeue();
                }
                return;
            }

            int noteIndex = 0;
            while (noteIndex < hittableQueue.Count)
            {
                var hittable = hittableQueue.At(noteIndex);
                var result = hittable.TryHit(currentInput.input, false);
                if (HandleStatus(result, hittable))
                {
                    while (noteIndex >= 0)
                    {
                        hittableQueue.Dequeue();
                        --noteIndex;
                    }
                    break;
                }
                ++noteIndex;
            }
        }

        protected abstract void DequeueMissedHittables(float position);

        protected override bool HandleStatus(HitStatus status, PlayableNote hittable)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    int index = notesOnScreen.Find(hittable.position.seconds);
                    if (index == 0)
                        notesOnScreen.Dequeue();
                    else if (index > 0)
                        notesOnScreen.RemoveAt(index);
                    else if (status == HitStatus.Hit)
                        viewableOverrides.Enqueue(hittable.position.seconds);
                    ++comboCount;
                    Debug.Log($"Test: Note hit - Combo: {comboCount}");
                    return true;
                default:
                    return false;
            }
        }

        protected void ApplyMiss()
        {
            comboCount = 0;
        }

        protected void CheckForOverhit()
        {
            if (overdriveIndex < overdrives.Count && currentInput.position.seconds >= overdrives[overdriveIndex].start.seconds)
            {

            }
        }

        protected void ProcessSoloes(float position)
        {
            while (soloIndex < soloes.Count)
            {
                var solo = soloes[soloIndex];
                if (position < solo.start.seconds)
                    break;

                if (!soloActive)
                {
                    soloActive = true;
                }

                if (position < solo.end.seconds)
                    break;

                Debug.Log($"Solo #{soloIndex} Completed - {100 * solo.Percentage}% | {solo.NotesHit}/{solo.NotesInPhrase}");
                // TODO - Add solo score
                // Run "End of solo" action w/ result
                soloActive = false;
                soloIndex++;
            }
        }

        protected virtual void AttachNewNote(long position, PlayableNote note)
        {
            AttachPhraseToNote(overdrives, overdriveIndex, position, note);
            AttachPhraseToNote(soloes, soloIndex, position, note);
        }

        private void AddToQueue(PlayableSemiQueue queue, float position, ref FlatMapNode<long, T> node)
        {
            var newNote = node.obj.ConvertToPlayable(new(node.key, position), sync, tempoIndex, positionTracker, noteTracker);
            AttachNewNote(node.key, newNote);
            queue.Enqueue(newNote);
            positionTracker = node.key;
            noteTracker = node.obj;
            Debug.Log("Note Added");
        }

        private static void AttachPhraseToNote<PhraseType>(List<PhraseType> phrases, int index, long position, PlayableNote note)
            where PhraseType : PlayablePhrase
        {
            while (index < phrases.Count)
            {
                var phr = phrases[index];
                if (position < phr.start.ticks)
                    break;

                if (position < phr.end.ticks)
                {
                    string attachment = note.AttachPhrase(phr);
                    if (attachment.Length > 0)
                        Debug.Log($"Position: {position} - Attached {attachment} to note");
                    break;
                }
                ++index;
            }
        }
    }
}
