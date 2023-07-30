using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Assets.Script.Types;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public interface INote
    {
        public int NumLanes { get; }
        public bool HasActiveNotes();
        public long GetLongestSustain();
#nullable enable
        public PlayableNote ConvertToPlayable(in long position, in SyncTrack sync, in long prevPosition, in INote? prevNote);
    }

    public enum HitStatus
    {
        Idle,
        Partial,
        Hit,
        Sustained,
        Dropped,
        Missed
    };

    public abstract class Player
    {
        protected readonly List<OverdrivePhrase> overdrives = new();
        protected int overdriveIndex = 0;
        protected bool overdriveActive = false;
        protected float overdrive = 0;
        protected float overdriveOffset = 0;

        protected readonly TimedSemiQueue<PlayableNote> notesOnScreen = new();
        protected (float, float) visibleNoteRange = new(1.0f, .3f);
        protected int nextViewable = 0;
        protected GameObject track;
        protected PlayerManager.Player inputHandler;
        protected SyncTrack sync;

        protected Player((GameObject, PlayerManager.Player) player, SyncTrack sync)
        {
            this.track = player.Item1;
            this.inputHandler = player.Item2;
            this.sync = sync;
        }

        public virtual void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is OverdrivePhrase overdrive)
                overdrives.Add(overdrive);
        }

        public void AddOverdrive(float overdrive)
        {
            Debug.Log($"Adding overdrive - {overdrive}");
            overdriveOffset += overdrive;
        }

        public void RemoveOverdrive(float overdrive)
        {
            Debug.Log($"Removing overdrive - {overdrive}");
            overdriveOffset -= overdrive;
        }

        public void IncrementOverdrive()
        {
            Debug.Log($"Incrementing overdrive index - {overdriveIndex++}");
        }

        protected void ApplyOverdriveOffset()
        {
            overdrive += overdriveOffset;
            overdrive = Math.Clamp(overdrive, 0, 1);
            if (overdriveOffset != 0)
                Debug.Log($"Overdrive - {overdrive}");
            overdriveOffset = 0;
        }

        public abstract void Update(float position);
        public abstract void Render(float position);

        protected abstract bool HandleStatus(HitStatus status, ref (float, PlayableNote) hittable);
    }

    public class Player_Instrument<T> : Player
        where T : class, INote, new()
    {
        private readonly TimedFlatMap<T> notes;
        private readonly float[] notePositions;

        private readonly TimedSemiQueue<PlayableNote> hittableQueue = new();
        private readonly (float, float) hitWindow = new(.065f, .065f);
        private int nextHittable = 0;
        private Queue<float> viewableOverrides = new();

        private List<SoloPhrase> soloes = new();
        private int soloIndex = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        private readonly PlayableNote[] sustainedNotes;
        private readonly List<(float, object)> inputSnapshots = new(2);
        private int comboCount = 0;

        private long positionTracker = 0;
        private T? noteTracker = null;

        public Player_Instrument((GameObject, PlayerManager.Player) player, TimedFlatMap<T> notes, float[] notePositions, SyncTrack sync) : base(player, sync)
        {
            this.notes = notes;
            this.notePositions = notePositions;
            sustainedNotes = new PlayableNote[5];
        }

        public override void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is SoloPhrase solo)
                soloes.Add(solo);
            else
                base.AttachPhrase(phrase);
        }

        public void UpdateSoloBox()
        {
            soloBoxUpdated = true;
        }

        public override void Update(float position)
        {
            EnqueueHittables(position);

            //var inputs = inputHandler.GetInputs();
            //for (int i = 0; i < inputs.Count; ++i)
            {
                (float, object) input = new(position, new());// inputs[i];
                TestForPendingDrops(input);
                if (hittableQueue.Count > 0)
                {
                    if (comboCount > 0)
                        ProcessInput_InCombo(input);
                    else
                        ProcessInput(input);
                }
                ApplyPendingDrops(input);
                CheckForOverhit(input);
            }

            DequeueMissedHittables(position);
            ApplyOverdriveOffset();

            ProcessSoloes(position);
            AdjustViewables(position);
        }

        public override void Render(float position)
        {
            
        }

        private void TestForPendingDrops((float, object) input)
        {

        }

        private void ApplyPendingDrops((float, object) input)
        {

        }

        private void EnqueueHittables(float position)
        {
            float startOfWindow = position + hitWindow.Item1;
            while (nextHittable < notes.Count)
            {
                float notePosition = notePositions[nextHittable];
                if (notePosition >= startOfWindow)
                    break;

                ref var node = ref notes.At_index(nextHittable);
                int index = notesOnScreen.Find(notePosition);
                if (index >= 0)
                    hittableQueue.Enqueue(notesOnScreen.At(index));
                else
                    AddToQueue(hittableQueue, notePosition, ref node);
                ++nextHittable;
            }
        }

        private void ProcessInput((float, object) input)
        {
            float endOfWindow = input.Item1 - hitWindow.Item2;
            while (hittableQueue.Count > 0 && hittableQueue.Peek().Item1 < endOfWindow)
                hittableQueue.Dequeue();

            int noteIndex = 0;
            while (noteIndex < hittableQueue.Count)
            {
                ref var hittable = ref hittableQueue.At(noteIndex);
                var result = hittable.Item2.TryHit(input.Item2, false, inputSnapshots);
                if (HandleStatus(result, ref hittable))
                {
                    while (noteIndex >= 0)
                    {
                        hittableQueue.Dequeue();
                        --noteIndex;
                    }
                    break;
                }
                else if (result == HitStatus.Idle)
                    break;
                ++noteIndex;
            }
        }

        private void ProcessInput_InCombo((float, object) input)
        {
            ref var hittable = ref hittableQueue.Peek();
            float lateHitLimit = hittable.Item1 + hitWindow.Item2;

            int nextIndex = nextHittable - hittableQueue.Count + 1;
            if (nextIndex < notes.Count)
            {
                long nextNote = notes.At_index(nextIndex).key;
                if (nextNote < lateHitLimit)
                    lateHitLimit = nextNote;
            }

            if (input.Item1 <= lateHitLimit)
            {
                var result = hittable.Item2.TryHit(input.Item2, true, inputSnapshots);
                if (HandleStatus(result, ref hittable))
                {
                    hittableQueue.Dequeue();
                }
            }
            else
            {
                hittableQueue.Dequeue();
                ApplyMiss();
            }
        }

        protected override bool HandleStatus(HitStatus status, ref (float, PlayableNote) hittable)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    {
                        int index = notesOnScreen.Find(hittable.Item1);
                        if (index == 0)
                            notesOnScreen.Dequeue();
                        else if (index > 0)
                            notesOnScreen.RemoveAt(index);
                        else if (status == HitStatus.Hit)
                            viewableOverrides.Enqueue(hittable.Item1);
                        ++comboCount;
                        Debug.Log("Test: Note hit");
                        return true;
                    }
                case HitStatus.Missed:
                    {
                        ApplyMiss();
                        break;
                    }
            }
            return false;
        }

        private void ApplyMiss()
        {
            comboCount = 0;
        }

        private void CheckForOverhit((float, object) input)
        {
            if (overdriveIndex < overdrives.Count && input.Item1 >= overdrives[overdriveIndex].start.seconds)
            {

            }
        }

        private void DequeueMissedHittables(float position)
        {
            float endOfWindow = position - hitWindow.Item2;
            while (hittableQueue.Count > 0 && hittableQueue.Peek().Item1 < endOfWindow)
            {
                ref var hittable = ref hittableQueue.Peek();
                if (hittable.Item1 >= endOfWindow)
                    break;

                if (comboCount > 0)
                {
                    // TODO process dropped combo
                }

                hittable.Item2.HandleMiss();
                hittableQueue.Dequeue();
            }
        }

        private void ProcessSoloes(float position)
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

        private void AdjustViewables(float position)
        {
            float startOfWindow = position + visibleNoteRange.Item1;
            float endOfWindow = position - visibleNoteRange.Item2;
            while (notesOnScreen.Count > 0 && notesOnScreen.Peek().Item1 < endOfWindow)
                notesOnScreen.Dequeue();

            while (nextViewable < notes.Count)
            {
                float notePosition = notePositions[nextViewable];
                if (notePosition >= startOfWindow)
                    break;

                if (notePosition >= endOfWindow)
                {
                    ref var node = ref notes.At_index(nextViewable);
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

        private void AddToQueue(TimedSemiQueue<PlayableNote> queue, float position, ref FlatMapNode<long, T> node)
        {
            var newNote = node.obj.ConvertToPlayable(node.key, sync, positionTracker, noteTracker);
            AttachPhraseToNote(overdrives, overdriveIndex, node.key, newNote);
            AttachPhraseToNote(soloes, soloIndex, node.key, newNote);
            queue.Enqueue(new(position, newNote));
            positionTracker = node.key;
            noteTracker = node.obj;
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
