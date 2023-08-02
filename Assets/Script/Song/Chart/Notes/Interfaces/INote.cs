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
        public PlayableNote ConvertToPlayable(DualPosition position, in SyncTrack sync, int syncIndex, in long prevPosition, in INote? prevNote);
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

    public enum OverdriveStyle
    {
        RockBand,
        GuitarHero
    }

    public abstract class Player
    {
        protected static SyncTrack sync = new();
        protected static (FlatMapNode<DualPosition, BeatStyle>[], int) beats;
        protected static OverdriveStyle style;
        protected const long OVERDRIVE_MAX = (long) int.MaxValue + 1;
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
        protected PlayerManager.Player inputHandler;

        protected int tempoIndex;

        protected Player((GameObject, PlayerManager.Player) player)
        {
            track = player.Item1;
            inputHandler = player.Item2;
            tempoIndex = 0;
        }

        public virtual void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is OverdrivePhrase overdrive)
                overdrives.Add(overdrive);
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
            Debug.Log($"Removing overdrive - {(float)overdrive / OVERDRIVE_MAX}");
            overdriveOffset -= overdrive;
        }

        public void IncrementOverdrive()
        {
            Debug.Log($"Incrementing overdrive index - {overdriveIndex++}");
        }

        protected void ApplyOverdriveOffset()
        {
            overdrive += overdriveOffset;
            overdrive = Math.Clamp(overdrive, 0, OVERDRIVE_MAX);
            if (overdriveOffset != 0)
                Debug.Log($"Overdrive - {(float)overdrive / OVERDRIVE_MAX}");

            overdriveOffset = 0;
            if (overdriveActive && overdrive == 0)
            {
                Debug.Log("Overdrive disabled");
                overdriveActive = false;
            }
        }

        protected void UpdateBeatsOnScreen(float position)
        {
            float startOfWindow = position + visibilityRange.Item1;
            float endOfWindow = position - visibilityRange.Item2;
            while (beatsOnScreen.Count > 0 && beatsOnScreen.Peek().Item1 < endOfWindow)
                beatsOnScreen.Dequeue();

            while (nextViewableBeat < beats.Item2)
            {
                ref var beat = ref beats.Item1[nextViewable];
                if (beat.key.seconds >= startOfWindow)
                    break;

                if (beat.key.seconds >= endOfWindow)
                    beatsOnScreen.Enqueue(new(beat.key.seconds, beat.obj));
                ++nextViewable;
            }
        }

        public abstract void Update(float position);
        public abstract void Render(float position);

        protected abstract bool HandleStatus(HitStatus status, PlayableNote hittable);
    }

    public class Player_Instrument<T> : Player
        where T : class, INote, new()
    {
        private readonly (FlatMapNode<long, T>[], int) notes;
        private readonly float[] notePositions;
        private readonly bool useShrinkingHitWindow = true;

        private readonly PlayableSemiQueue hittableQueue = new();
        private readonly (float, float) hitWindow = new(.065f, .065f);
        private int nextHittable = 0;
        private Queue<float> viewableOverrides = new();

        private List<SoloPhrase> soloes = new();
        private int soloIndex = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        private readonly List<PlayableNote> sustainedNotes = new();
        private readonly List<PlayableNote> pendingDrop = new();
        private readonly List<PlayableNote> droppedNotes = new();
        private int comboCount = 0;

        private long positionTracker = 0;
        private T? noteTracker = null;
        private object currentInput = new();

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

        public void UpdateSoloBox()
        {
            soloBoxUpdated = true;
        }

        public override void Update(float position)
        {
            EnqueueHittables(position);

            //(float position, object input)[] inputs = inputHandler.GetInputs();
            //for (int i = 0; i < inputs.Length; ++i)
            //{
            //    DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), inputs[i].position);
            //    ProcessOverdriveAndSustains(ref dual, ref inputs[i].input);
            //    ProcessInput(ref dual, ref inputs[i].input);
            //    HandleDrops(ref dual, ref inputs[i].input);
            //    CheckForOverhit(ref dual, ref inputs[i].input);
            //}

            // TESTING VERSION
            while (hittableQueue.Count > 0)
            {
                var dual = hittableQueue.Peek().position;
                if (dual.seconds > position)
                    break;

                object input = new();
                ProcessOverdriveAndSustains(ref dual, ref input);
                ProcessInput(ref dual, ref input);
                TryActivateOverdrive(ref dual, ref input);
                HandleDrops(ref dual, ref input);
                CheckForOverhit(ref dual, ref input);
            }

            DequeueMissedHittables(position);
            {
                DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), position);
                ProcessOverdriveAndSustains(ref dual, ref currentInput);
            }
            ProcessSoloes(position);
            AdjustNotesOnScreen(position);
        }

        public override void Render(float position)
        {
            
        }

        private void ProcessOverdriveAndSustains(ref DualPosition position, ref object input)
        {
            DrainOverdrive(ref position, ref input);
            AdjustSustains(ref position, ref input);
            ApplyOverdriveOffset();
        }

        private void DrainOverdrive(ref DualPosition position, ref object input)
        {
            if (!TryActivateOverdrive(ref position, ref input))
            {
                long posTicks = position.ticks;
                
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
                        long pivot = posTicks < nextBeat ? posTicks : nextBeat;
                        RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                        overdriveTime = pivot;

                        if (posTicks < nextBeat || nextIndex + 1 == beats.Item2)
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
                        long pivot = posTicks < nextMeasure ? posTicks : nextMeasure;
                        RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                        overdriveTime = pivot;

                        if (posTicks < nextMeasure || nextIndex + 1 == beats.Item2)
                            break;

                        currentBeatIndex_overdrive = nextIndex;
                    }
                }
            }
        }

        protected virtual bool TryActivateOverdrive(ref DualPosition position, ref object input)
        {
            if (!overdriveActive)
            {
                //                                    TEST ACTIVATION
                if (overdrive >= OVERDRIVE_MAX / 2 && true)
                {
                    overdriveActive = true;
                    overdriveTime = position.ticks;
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
                return true;
            }
            return false;
        }

        private void AdjustSustains(ref DualPosition position, ref object input)
        {
            for (int i = 0; i < sustainedNotes.Count;)
            {
                var note = sustainedNotes[i];
                var result = note.UpdateSustain(position, ref input);

                if (result != HitStatus.Dropped)
                {
                    // Add score
                }
                else
                {
                    pendingDrop.Add(note);
                    Debug.Log("Test: Pending drop added");
                }

                if (result != HitStatus.Sustained)
                {
                    sustainedNotes.RemoveAt(i);
                    Debug.Log("Test: Sustained removed");
                }
                else
                    ++i;
            }
        }

        private void HandleDrops(ref DualPosition position, ref object input)
        {
            for (int i = 0; i < pendingDrop.Count;)
            {
                var note = pendingDrop[i];
                var result = note.UpdateSustain(position, ref input);
                if (result != HitStatus.Dropped)
                {
                    // TODO - Add score
                    Debug.Log("Test: Drop overruled");
                    if (result == HitStatus.Sustained)
                    {
                        sustainedNotes.Add(note);
                        Debug.Log("Test: Drop -> Sustain");
                    }
                    else
                        Debug.Log("Test: Drop -> Hit");
                }
                else
                {
                    droppedNotes.Add(note);
                    Debug.Log("Test: Drop confirmed");
                }
            }
            pendingDrop.Clear();

            float endOfWindow = position.seconds - visibilityRange.Item2;
            for (int i = 0; i < droppedNotes.Count;)
            {
                if (endOfWindow >= droppedNotes[i].End.seconds)
                {
                    droppedNotes.RemoveAt(i);
                    Debug.Log("Test: Dropped removed");
                }
                else
                    ++i;
            }
        }

        private void EnqueueHittables(float position)
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

        private void ProcessInput(ref DualPosition position, ref object input)
        {
            DequeueMissedHittables(position.seconds);
            if (comboCount > 0)
            {
                if (hittableQueue.Count > 0)
                {
                    var hittable = hittableQueue.Peek();
                    var result = hittable.TryHit(ref input, true);
                    if (HandleStatus(result, hittable))
                        hittableQueue.Dequeue();
                }
                return;
            }

            int noteIndex = 0;
            while (noteIndex < hittableQueue.Count)
            {
                var hittable = hittableQueue.At(noteIndex);
                var result = hittable.TryHit(ref input, false);
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

                    if (status == HitStatus.Sustained)
                    {
                        sustainedNotes.Add(hittable);
                        Debug.Log("Test: Sustained added");
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyMiss()
        {
            comboCount = 0;
        }

        private void CheckForOverhit(ref DualPosition position, ref object input)
        {
            if (overdriveIndex < overdrives.Count && position.seconds >= overdrives[overdriveIndex].start.seconds)
            {

            }
        }

        private void DequeueMissedHittables(float position)
        {
            float endOfWindow = position - hitWindow.Item2;
            while (hittableQueue.Count > 0)
            {
                var hittable = hittableQueue.Peek();
                if (useShrinkingHitWindow && comboCount > 0)
                {
                    int nextIndex = nextHittable - hittableQueue.Count + 1;
                    if (nextIndex == notePositions.Length || position < notePositions[nextIndex])
                        break;
                }
                else if (hittable.position.seconds >= endOfWindow)
                    break;

                if (hittable.OnDequeueFromMiss() == HitStatus.Dropped)
                    droppedNotes.Add(hittable);
                hittableQueue.Dequeue();
                ApplyMiss();
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

        private void AdjustNotesOnScreen(float position)
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

        private void AddToQueue(PlayableSemiQueue queue, float position, ref FlatMapNode<long, T> node)
        {
            var newNote = node.obj.ConvertToPlayable(new(node.key, position), sync, tempoIndex, positionTracker, noteTracker);
            AttachPhraseToNote(overdrives, overdriveIndex, node.key, newNote);
            AttachPhraseToNote(soloes, soloIndex, node.key, newNote);
            queue.Enqueue(newNote);
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

    public class Player_Drums<T> : Player_Instrument<T>
        where T : DrumNote, new()
    {
        private readonly List<OverdrivePhrase> overdrives = new();
        public Player_Drums((GameObject, PlayerManager.Player) player, (FlatMapNode<long, T>[], int) notes, float[] notePositions) : base(player, notes, notePositions) { }

        public override void Update(float position)
        {
            EnqueueHittables(position);

            //(float position, object input)[] inputs = inputHandler.GetInputs();
            //for (int i = 0; i < inputs.Length; ++i)
            //{
            //    DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), inputs[i].position);
            //    ProcessOverdriveAndSustains(ref dual, ref inputs[i].input);
            //    ProcessInput(ref dual, ref inputs[i].input);
            //    HandleDrops(ref dual, ref inputs[i].input);
            //    CheckForOverhit(ref dual, ref inputs[i].input);
            //}

            // TESTING VERSION
            while (hittableQueue.Count > 0)
            {
                var dual = hittableQueue.Peek().position;
                if (dual.seconds > position)
                    break;

                object input = new();
                ProcessOverdriveAndSustains(ref dual, ref input);
                ProcessInput(ref dual, ref input);
                TryActivateOverdrive(ref dual, ref input);
                HandleDrops(ref dual, ref input);
                CheckForOverhit(ref dual, ref input);
            }

            DequeueMissedHittables(position);
            {
                DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), position);
                ProcessOverdriveAndSustains(ref dual, ref currentInput);
            }
            ProcessSoloes(position);
            AdjustNotesOnScreen(position);
        }

        public override void Render(float position)
        {

        }

        private void ProcessOverdriveAndSustains(ref DualPosition position, ref object input)
        {
            DrainOverdrive(ref position, ref input);
            AdjustSustains(ref position, ref input);
            ApplyOverdriveOffset();
        }

        private void DrainOverdrive(ref DualPosition position, ref object input)
        {
            if (!TryActivateOverdrive(ref position, ref input))
            {
                long posTicks = position.ticks;

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
                        long pivot = posTicks < nextBeat ? posTicks : nextBeat;
                        RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                        overdriveTime = pivot;

                        if (posTicks < nextBeat || nextIndex + 1 == beats.Item2)
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
                        long pivot = posTicks < nextMeasure ? posTicks : nextMeasure;
                        RemoveOverdrive((long) (overdrivePerTick * (pivot - overdriveTime)));
                        overdriveTime = pivot;

                        if (posTicks < nextMeasure || nextIndex + 1 == beats.Item2)
                            break;

                        currentBeatIndex_overdrive = nextIndex;
                    }
                }
            }
        }

        protected override bool TryActivateOverdrive(ref DualPosition position, ref object input)
        {
            if (!overdriveActive)
            {
                //                                    TEST ACTIVATION
                if (overdrive >= OVERDRIVE_MAX / 2 && true)
                {
                    overdriveActive = true;
                    overdriveTime = position.ticks;
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
                return true;
            }
            return false;
        }

        private void AdjustSustains(ref DualPosition position, ref object input)
        {
            for (int i = 0; i < sustainedNotes.Count;)
            {
                var note = sustainedNotes[i];
                var result = note.UpdateSustain(position, ref input);

                if (result != HitStatus.Dropped)
                {
                    // Add score
                }
                else
                {
                    pendingDrop.Add(note);
                    Debug.Log("Test: Pending drop added");
                }

                if (result != HitStatus.Sustained)
                {
                    sustainedNotes.RemoveAt(i);
                    Debug.Log("Test: Sustained removed");
                }
                else
                    ++i;
            }
        }

        private void HandleDrops(ref DualPosition position, ref object input)
        {
            for (int i = 0; i < pendingDrop.Count;)
            {
                var note = pendingDrop[i];
                var result = note.UpdateSustain(position, ref input);
                if (result != HitStatus.Dropped)
                {
                    // TODO - Add score
                    Debug.Log("Test: Drop overruled");
                    if (result == HitStatus.Sustained)
                    {
                        sustainedNotes.Add(note);
                        Debug.Log("Test: Drop -> Sustain");
                    }
                    else
                        Debug.Log("Test: Drop -> Hit");
                }
                else
                {
                    droppedNotes.Add(note);
                    Debug.Log("Test: Drop confirmed");
                }
            }
            pendingDrop.Clear();

            float endOfWindow = position.seconds - visibilityRange.Item2;
            for (int i = 0; i < droppedNotes.Count;)
            {
                if (endOfWindow >= droppedNotes[i].End.seconds)
                {
                    droppedNotes.RemoveAt(i);
                    Debug.Log("Test: Dropped removed");
                }
                else
                    ++i;
            }
        }

        private void EnqueueHittables(float position)
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

        private void ProcessInput(ref DualPosition position, ref object input)
        {
            DequeueMissedHittables(position.seconds);
            if (comboCount > 0)
            {
                if (hittableQueue.Count > 0)
                {
                    var hittable = hittableQueue.Peek();
                    var result = hittable.TryHit(ref input, true);
                    if (HandleStatus(result, hittable))
                        hittableQueue.Dequeue();
                }
                return;
            }

            int noteIndex = 0;
            while (noteIndex < hittableQueue.Count)
            {
                var hittable = hittableQueue.At(noteIndex);
                var result = hittable.TryHit(ref input, false);
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

                    if (status == HitStatus.Sustained)
                    {
                        sustainedNotes.Add(hittable);
                        Debug.Log("Test: Sustained added");
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void ApplyMiss()
        {
            comboCount = 0;
        }

        private void CheckForOverhit(ref DualPosition position, ref object input)
        {
            if (overdriveIndex < overdrives.Count && position.seconds >= overdrives[overdriveIndex].start.seconds)
            {

            }
        }

        private void DequeueMissedHittables(float position)
        {
            float endOfWindow = position - hitWindow.Item2;
            while (hittableQueue.Count > 0)
            {
                var hittable = hittableQueue.Peek();
                if (useShrinkingHitWindow && comboCount > 0)
                {
                    int nextIndex = nextHittable - hittableQueue.Count + 1;
                    if (nextIndex == notePositions.Length || position < notePositions[nextIndex])
                        break;
                }
                else if (hittable.position.seconds >= endOfWindow)
                    break;

                if (hittable.OnDequeueFromMiss() == HitStatus.Dropped)
                    droppedNotes.Add(hittable);
                hittableQueue.Dequeue();
                ApplyMiss();
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

        private void AdjustNotesOnScreen(float position)
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

        private void AddToQueue(PlayableSemiQueue queue, float position, ref FlatMapNode<long, T> node)
        {
            var newNote = node.obj.ConvertToPlayable(new(node.key, position), sync, tempoIndex, positionTracker, noteTracker);
            AttachPhraseToNote(overdrives, overdriveIndex, node.key, newNote);
            AttachPhraseToNote(soloes, soloIndex, node.key, newNote);
            queue.Enqueue(newNote);
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
