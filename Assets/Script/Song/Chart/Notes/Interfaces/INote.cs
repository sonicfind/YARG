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
    public interface INote_Base
    {
        public bool HasActiveNotes();
        public ulong GetLongestSustain();
    }
    public interface INote : INote_Base
    {
#nullable enable
        public IPlayableNote ConvertToPlayable(in ulong position, in SyncTrack sync, in ulong prevPosition, in INote? prevNote);
    }

    public unsafe interface INote_S : INote_Base
    {
        public uint NumLanes { get; }
        public IPlayableNote ConvertToPlayable<T>(in ulong position, in SyncTrack sync, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S;
    }

    public class InputHandler
    {
        public readonly int laneCount;

        private List<(float, object)> inputs;
        private readonly object inputLock;

        public InputHandler(int laneCount)
        {
            inputs = new();
            inputLock = new();
            this.laneCount = laneCount;
        }

        public List<(float, object)> GetInputs()
        {
            var inputs = this.inputs;
            lock (inputLock) this.inputs = new();
            return inputs;
        }
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
        protected readonly List<(float, OverdrivePhrase)> overdrives = new();
        protected int overdriveIndex = 0;
        protected float overdrive = 0;
        protected float overdriveOffset = 0;

        protected readonly TimedSemiQueue<IPlayableNote> notesOnScreen = new();
        protected (float, float) visibleNoteRange = new(1.5f, .5f);
        protected int nextViewable = 0;
        protected InputHandler inputHandler;
        protected SyncTrack sync;

        protected Player(InputHandler inputHandler, SyncTrack sync)
        {
            this.inputHandler = inputHandler;
            this.sync = sync;
        }

        public virtual void AttachPhrase(float position, float end, PlaytimePhrase phrase)
        {
            if (phrase is OverdrivePhrase overdrive)
                overdrives.Add(new(position, overdrive));
        }

        public void AddOverdrive(float overdrive)
        {
            overdriveOffset += overdrive;
        }

        public void RemoveOverdrive(float overdrive)
        {
            overdriveOffset -= overdrive;
        }

        public void IncrementOverdrive()
        {
            overdriveIndex++;
        }

        public abstract void Update(float position);

        protected abstract bool HandleStatus(HitStatus status, ref (float, IPlayableNote) hittable);
    }

    public abstract class Player_Instrument_Base<T> : Player
        where T : INote_Base
    {
        private List<(float, float, SoloPhrase<T>)> soloes = new();
        private int soloIndex = 0;
        private float soloPercentage = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        protected readonly FlatMap_Base<ulong, T> notes;
        protected readonly float[] notePositions;
        private readonly TimedSemiQueue<IPlayableNote> hittableQueue = new();
        private readonly (float, float) hitWindow = new(.065f, .065f);
        private int nextHittable = 0;
        private float priorHit = 0;

        private readonly IPlayableNote[] sustainedNotes;
        private readonly List<(float, object)> inputSnapshots = new(2);
        private int comboCount = 0;

        protected Player_Instrument_Base(InputHandler inputHandler, FlatMap_Base<ulong, T> notes, float[] notePositions, SyncTrack sync) : base(inputHandler, sync)
        {
            this.notes = notes;
            this.notePositions = notePositions;
            sustainedNotes = new IPlayableNote[inputHandler.laneCount];
        }

        public override void AttachPhrase(float position, float end, PlaytimePhrase phrase)
        {
            if (phrase is SoloPhrase<T> solo)
                soloes.Add(new(position, end, solo));
            else
                base.AttachPhrase(position, end, phrase);
        }

        public void UpdateSolo(float percentage)
        {
            soloBoxUpdated = true;
            soloPercentage = percentage;
        }

        public override void Update(float position)
        {
            EnqueueHittables(position);

            var inputs = inputHandler.GetInputs();
            foreach (var input in inputs)
            {
                TestForPendingDrops(input);
                if (hittableQueue.Count > 0)
                {
                    if (comboCount > 0)
                        ProcessInput_InCombo(input);
                    else
                        ProcessInput(input);
                }
                ApplyPendingDrops(input);
            }

            float endOfWindow = position - hitWindow.Item2;
            while (hittableQueue.Count > 0 && hittableQueue.Peek().Item1 < endOfWindow)
            {
                if (comboCount > 0)
                {
                    // TODO process dropped combo
                }
                Debug.Log($"Position: {hittableQueue.Peek().Item1} - Note removed from queue");
                hittableQueue.Dequeue();
            }


            while (soloIndex < soloes.Count && soloes[soloIndex].Item1 <= position)
            {
                if (!soloActive)
                {
                    soloActive = true;
                    soloPercentage = 0;
                }

                if (position < soloes[soloIndex].Item2)
                    break;

                // TODO - Add solo score
                // Run "End of solo" action w/ result
                soloActive = false;
                soloIndex++;
            }

            if (overdriveIndex < overdrives.Count && position >= overdrives[overdriveIndex].Item1)


                overdrive += overdriveOffset;
            Math.Clamp(overdrive, 0, 1);
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

        private void EnqueueViewables(ulong position)
        {
            float startOfWindow = position + visibleNoteRange.Item1;
            while (nextViewable < notes.Count)
            {
                float notePosition = notePositions[nextViewable];
                if (notePosition >= startOfWindow)
                    break;

                ref var node = ref notes.At_index(nextViewable);
                int index = hittableQueue.Find(notePosition);
                if (index >= 0)
                    notesOnScreen.Enqueue(hittableQueue.At(index));
                else
                    AddToQueue(notesOnScreen, notePosition, ref node);
                ++nextViewable;
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
                var result = hittable.Item2.TryHit(input.Item2, inputSnapshots);
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
                ulong nextNote = notes.At_index(nextIndex).key;
                if (nextNote < lateHitLimit)
                    lateHitLimit = nextNote;
            }

            if (input.Item1 <= lateHitLimit)
            {
                var result = hittable.Item2.TryHit_InCombo(input.Item2, inputSnapshots);
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

        protected override bool HandleStatus(HitStatus status, ref (float, IPlayableNote) hittable)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    {
                        priorHit = hittable.Item1;
                        int index = notesOnScreen.Find(hittable.Item1);
                        if (index == 0)
                            notesOnScreen.Dequeue();
                        else if (index > 0)
                            notesOnScreen.RemoveAt(index);
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

        protected abstract void AddToQueue(TimedSemiQueue<IPlayableNote> queue, float position, ref FlatMapNode<ulong, T> node);
    }

    public class Player_Instrument<T> : Player_Instrument_Base<T>
        where T : class, INote, new()
    {
        private ulong positionTracker = 0;
        private T? noteTracker = null;
        public Player_Instrument(InputHandler inputHandler, TimedFlatMap<T> notes, float[] notePositions, SyncTrack sync) : base(inputHandler, notes, notePositions, sync) { }

        protected override void AddToQueue(TimedSemiQueue<IPlayableNote> queue, float position, ref FlatMapNode<ulong, T> node)
        {
            queue.Enqueue(new(position, node.obj.ConvertToPlayable(node.key, sync, positionTracker, noteTracker)));
            positionTracker = node.key;
            noteTracker = node.obj;

            Debug.Log($"Position: {position} - Note added to queue");
        }
    }

    public unsafe class Player_Instrument_S<T> : Player_Instrument_Base<T>
        where T : unmanaged, INote_S
    {
        private ulong positionTracker = 0;
        private T* noteTracker = null;
        public Player_Instrument_S(InputHandler inputHandler, TimedNativeFlatMap<T> notes, float[] notePositions, SyncTrack sync) : base(inputHandler, notes, notePositions, sync) { }

        protected override void AddToQueue(TimedSemiQueue<IPlayableNote> queue, float position, ref FlatMapNode<ulong, T> node)
        {
            queue.Enqueue(new(position, node.obj.ConvertToPlayable(node.key, sync, positionTracker, noteTracker)));
            positionTracker = node.key;

            // The node's array buffer is a fixed location, so this is safe
            fixed (FlatMapNode<ulong, T>* ptr = &node)
                noteTracker = &ptr->obj;
            Debug.Log($"Position: {position} - Note added to queue");
        }
    }

    public readonly struct SubNote
    {
        public readonly int index;
        public readonly ulong endTick;
        public SubNote(int index, ulong endTick)
        {
            this.index = index;
            this.endTick = endTick;
        }
    }

    public interface IPlayableNote
    {
        public void AttachPhrase(PlaytimePhrase phrase);
        public HitStatus TryHit(object input, in List<(float, object)> inputSnapshots);
        public HitStatus TryHit_InCombo(object input, in List<(float, object)> inputSnapshots);

        public (HitStatus, int) UpdateSustain(object input, in List<(float, object)> inputSnapshots);
    }
}
