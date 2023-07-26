using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;
using static UnityEditor.ShaderGraph.Internal.KeywordDependentCollection;

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
        public IPlayableNote ConvertToPlayable(in ulong position, in ulong prevPosition, in INote? prevNote);
    }

    public unsafe interface INote_S : INote_Base
    {
        public uint NumLanes { get; }
        public IPlayableNote ConvertToPlayable<T>(in ulong position, in ulong prevPosition, in T* prevNote)
            where T : unmanaged, INote_S;
    }

    public class InputHandler
    {
        public readonly int laneCount;

        private List<(ulong, object)> inputs;
        private readonly object inputLock;

        public InputHandler(int laneCount)
        {
            inputs = new();
            inputLock = new();
            this.laneCount = laneCount;
        }

        public List<(ulong, object)> GetInputs()
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
        protected readonly List<(ulong, OverdrivePhrase)> overdrives = new();
        protected int overdriveIndex = 0;
        protected float overdrive = 0;
        protected float overdriveOffset = 0;

        protected (int, int) visibleNoteRange = new(0, 0);
        protected InputHandler inputHandler;

        protected Player(InputHandler inputHandler)
        {
            this.inputHandler = inputHandler;
        }

        public virtual void AttachPhrase(ulong position, ulong end, PlaytimePhrase phrase)
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

        public abstract void Update(ulong position);

        protected abstract bool HandleStatus(HitStatus status);
    }

    public abstract class Player_Instrument_Base<T> : Player
        where T : INote_Base, new()
    {
        private List<(ulong, ulong, SoloPhrase)> soloes = new();
        private int soloIndex = 0;
        private float soloPercentage = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        private readonly FlatMap_Base<ulong, T> note_buffer;

        private readonly Queue<(ulong, IPlayableNote)> hittableQueue;
        private readonly (ulong, ulong) hitWindow = new(55, 55);
        private (int, int) hittableNotes = new(0, 0);

        private readonly IPlayableNote[] sustainedNotes;
        private readonly List<(ulong, object)> inputSnapshots = new(2);
        private int comboCount = 0;

        public Player_Instrument_Base(InputHandler inputHandler, FlatMap_Base<ulong, T> notes) : base(inputHandler)
        {
            note_buffer = notes;
            sustainedNotes = new IPlayableNote[inputHandler.laneCount];
        }

        public override void AttachPhrase(ulong position, ulong end, PlaytimePhrase phrase)
        {
            if (phrase is SoloPhrase solo)
                soloes.Add(new(position, end, solo));
            else
                base.AttachPhrase(position, end, phrase);
        }

        public void UpdateSolo(float percentage)
        {
            soloBoxUpdated = true;
            soloPercentage = percentage;
        }

        public override void Update(ulong position)
        {
            ulong startOfWindow = position + hitWindow.Item1;
            while (hittableNotes.Item2 < notes.Count && notes.At_index(hittableNotes.Item2).key < startOfWindow)
                ++hittableNotes.Item2;

            var inputs = inputHandler.GetInputs();
            int inputIndex = 0;
            while (inputIndex < inputs.Count && hittableNotes.Item1 < hittableNotes.Item2)
            {
                var input = inputs[inputIndex];

                if (comboCount > 0)
                {
                    int noteIndex = hittableNotes.Item1;
                    ulong lateHitLimit = notes.At_index(noteIndex).key + hitWindow.Item2;
                    if (noteIndex + 1 < notes.Count)
                    {
                        ulong nextNote = notes.At_index(noteIndex + 1).key;
                        if (nextNote < lateHitLimit)
                            lateHitLimit = nextNote;
                    }

                    var result = HitStatus.Missed;
                    if (input.Item1 <= lateHitLimit)
                        result = notes[noteIndex].Item2.TryHit_InCombo(input.Item2, inputSnapshots);
                    HandleStatus(result);
                }
                else
                {
                    while (hittableNotes.Item1 < hittableNotes.Item2 && input.Item1 <= notes[hittableNotes.Item1].Item1 + hitWindow.Item2)
                        ++hittableNotes.Item1;

                    int noteIndex = hittableNotes.Item1;
                    while (noteIndex < hittableNotes.Item2)
                    {
                        var result = notes[noteIndex].Item2.TryHit(input.Item2, inputSnapshots);
                        if (HandleStatus(result))
                        {

                            hittableNotes.Item2 = noteIndex + 1;
                            break;
                        }
                        else if (result == HitStatus.Idle)
                            break;
                        ++noteIndex;
                    }
                }

            }

            if (soloIndex < soloes.Count && soloes[soloIndex].Item1 <= position)
            {
                if (!soloActive)
                    soloActive = true;
                else if (soloes[soloIndex].Item2 <= position)
                {
                    soloActive = false;
                    while (true)
                    {
                        // TODO - Add solo score
                        soloIndex++;
                        if (soloIndex >= soloes.Count || position < soloes[soloIndex].Item1)
                        {
                            // Run "End of solo" action w/ result
                            int solo = soloIndex - 1;
                            break;
                        }

                        if (position < soloes[soloIndex].Item2)
                        {
                            soloActive = true;
                            break;
                        }
                    }
                }
            }

            if (overdriveIndex < overdrives.Count && position >= overdrives[overdriveIndex].Item1)


                overdrive += overdriveOffset;
            Math.Clamp(overdrive, 0, 1);
        }

        protected override bool HandleStatus(HitStatus status)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    {
                        hittableNotes.Item1++;
                        return true;
                    }
                case HitStatus.Missed:
                    {
                        comboCount = 0;
                        break;
                    }
            }
            return false;
        }
    }

    public unsafe class Player_Instrument<T> : Player
        where T : INote_Base
    {
        private readonly FlatMapNode<ulong, T>;
        private readonly * notes;
        private readonly int numNotes;


        private readonly (ulong, ulong) hitWindow = new(55, 55);
        private (int, int) hittableNotes = new(0, 0);

        private readonly IPlayableNote[] sustainedNotes;
        private readonly List<(ulong, object)> inputSnapshots = new(2);
        private int comboCount = 0;

        public Player_Instrument(FlatMapNode<ulong, T>[] notes, InputHandler inputHandler) : base(inputHandler)
        {
            handle = GCHandle.Alloc(notes, GCHandleType.Pinned);
            this.notes = (FlatMapNode<ulong, T>*) handle.AddrOfPinnedObject();
            numNotes = notes.Length;

            this.inputHandler = inputHandler;
            sustainedNotes = new IPlayableNote[inputHandler.laneCount];
        }

        public Player_Instrument(FlatMapNode<ulong, T>* notes, int length, InputHandler inputHandler) : base(inputHandler)
        {
            this.notes = notes;
            this.inputHandler = inputHandler;
            sustainedNotes = new IPlayableNote[inputHandler.laneCount];
        }

        public override void AttachPhrase(ulong position, ulong end, PlaytimePhrase phrase)
        {
            if (phrase is SoloPhrase solo)
                soloes.Add(new(position, end, solo));
            else
                base.AttachPhrase(position, end, phrase);
        }

        public void UpdateSolo(float percentage)
        {
            soloBoxUpdated = true;
            soloPercentage = percentage;
        }

        public override void Update(ulong position)
        {
            ulong startOfWindow = position + hitWindow.Item1;
            while (hittableNotes.Item2 < notes.Length && notes[hittableNotes.Item2].Item1 < startOfWindow)
                ++hittableNotes.Item2;

            var inputs = inputHandler.GetInputs();
            int inputIndex = 0;
            while (inputIndex < inputs.Count && hittableNotes.Item1 < hittableNotes.Item2)
            {
                var input = inputs[inputIndex];

                if (comboCount > 0)
                {
                    int noteIndex = hittableNotes.Item1;
                    ulong lateHitLimit = notes[noteIndex].Item1 + hitWindow.Item2;
                    if (noteIndex + 1 < notes.Length)
                    {
                        ulong nextNote = notes[noteIndex + 1].Item1;
                        if (nextNote < lateHitLimit)
                            lateHitLimit = nextNote;
                    }

                    var result = HitStatus.Missed;
                    if (input.Item1 <= lateHitLimit)
                        result = notes[noteIndex].Item2.TryHit_InCombo(input.Item2, inputSnapshots);
                    HandleStatus(result);
                }
                else
                {
                    while (hittableNotes.Item1 < hittableNotes.Item2 && input.Item1 <= notes[hittableNotes.Item1].Item1 + hitWindow.Item2)
                        ++hittableNotes.Item1;

                    int noteIndex = hittableNotes.Item1;
                    while (noteIndex < hittableNotes.Item2)
                    {
                        var result = notes[noteIndex].Item2.TryHit(input.Item2, inputSnapshots);
                        if (HandleStatus(result))
                        {

                            hittableNotes.Item2 = noteIndex + 1;
                            break;
                        }
                        else if (result == HitStatus.Idle)
                            break;
                        ++noteIndex;
                    }
                }

            }

            if (soloIndex < soloes.Count && soloes[soloIndex].Item1 <= position)
            {
                if (!soloActive)
                    soloActive = true;
                else if (soloes[soloIndex].Item2 <= position)
                {
                    soloActive = false;
                    while (true)
                    {
                        // TODO - Add solo score
                        soloIndex++;
                        if (soloIndex >= soloes.Count || position < soloes[soloIndex].Item1)
                        {
                            // Run "End of solo" action w/ result
                            int solo = soloIndex - 1;
                            break;
                        }

                        if (position < soloes[soloIndex].Item2)
                        {
                            soloActive = true;
                            break;
                        }
                    }
                }
            }

            if (overdriveIndex < overdrives.Count && position >= overdrives[overdriveIndex].Item1)


                overdrive += overdriveOffset;
            Math.Clamp(overdrive, 0, 1);
        }

        protected override bool HandleStatus(HitStatus status)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    {
                        hittableNotes.Item1++;
                        return true;
                    }
                case HitStatus.Missed:
                    {
                        comboCount = 0;
                        break;
                    }
            }
            return false;
        }
    }

    public unsafe class Player_Instrument_S<T> : Player
        where T : unmanaged, INote_S
    {
        private List<(ulong, ulong, SoloPhrase)> soloes = new();
        private int soloIndex = 0;
        private float soloPercentage = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        private readonly FlatMapNode<ulong, T>* notes;
        protected readonly int numNotes;


        private readonly (ulong, ulong) hitWindow = new(55, 55);
        private (int, int) hittableNotes = new(0, 0);

        private readonly IPlayableNote[] sustainedNotes;
        private readonly List<(ulong, object)> inputSnapshots = new(2);
        private int comboCount = 0;

        public Player_Instrument_S(FlatMapNode<ulong, T>* notes, int length, InputHandler inputHandler) : base(inputHandler)
        {
            this.notes = notes;
            this.inputHandler = inputHandler;
            sustainedNotes = new IPlayableNote[inputHandler.laneCount];
        }

        public override void AttachPhrase(ulong position, ulong end, PlaytimePhrase phrase)
        {
            if (phrase is SoloPhrase solo)
                soloes.Add(new(position, end, solo));
            else
                base.AttachPhrase(position, end, phrase);
        }

        public void UpdateSolo(float percentage)
        {
            soloBoxUpdated = true;
            soloPercentage = percentage;
        }

        public override void Update(ulong position)
        {
            ulong startOfWindow = position + hitWindow.Item1;
            while (hittableNotes.Item2 < notes.Length && notes[hittableNotes.Item2].Item1 < startOfWindow)
                ++hittableNotes.Item2;

            var inputs = inputHandler.GetInputs();
            int inputIndex = 0;
            while (inputIndex < inputs.Count && hittableNotes.Item1 < hittableNotes.Item2)
            {
                var input = inputs[inputIndex];

                if (comboCount > 0)
                {
                    int noteIndex = hittableNotes.Item1;
                    ulong lateHitLimit = notes[noteIndex].Item1 + hitWindow.Item2;
                    if (noteIndex + 1 < notes.Length)
                    {
                        ulong nextNote = notes[noteIndex + 1].Item1;
                        if (nextNote < lateHitLimit)
                            lateHitLimit = nextNote;
                    }

                    var result = HitStatus.Missed;
                    if (input.Item1 <= lateHitLimit)
                        result = notes[noteIndex].Item2.TryHit_InCombo(input.Item2, inputSnapshots);
                    HandleStatus(result);
                }
                else
                {
                    while (hittableNotes.Item1 < hittableNotes.Item2 && input.Item1 <= notes[hittableNotes.Item1].Item1 + hitWindow.Item2)
                        ++hittableNotes.Item1;

                    int noteIndex = hittableNotes.Item1;
                    while (noteIndex < hittableNotes.Item2)
                    {
                        var result = notes[noteIndex].Item2.TryHit(input.Item2, inputSnapshots);
                        if (HandleStatus(result))
                        {

                            hittableNotes.Item2 = noteIndex + 1;
                            break;
                        }
                        else if (result == HitStatus.Idle)
                            break;
                        ++noteIndex;
                    }
                }

            }

            if (soloIndex < soloes.Count && soloes[soloIndex].Item1 <= position)
            {
                if (!soloActive)
                    soloActive = true;
                else if (soloes[soloIndex].Item2 <= position)
                {
                    soloActive = false;
                    while (true)
                    {
                        // TODO - Add solo score
                        soloIndex++;
                        if (soloIndex >= soloes.Count || position < soloes[soloIndex].Item1)
                        {
                            // Run "End of solo" action w/ result
                            int solo = soloIndex - 1;
                            break;
                        }

                        if (position < soloes[soloIndex].Item2)
                        {
                            soloActive = true;
                            break;
                        }
                    }
                }
            }

            if (overdriveIndex < overdrives.Count && position >= overdrives[overdriveIndex].Item1)


                overdrive += overdriveOffset;
            Math.Clamp(overdrive, 0, 1);
        }

        protected override bool HandleStatus(HitStatus status)
        {
            switch (status)
            {
                case HitStatus.Sustained:
                case HitStatus.Hit:
                    {
                        hittableNotes.Item1++;
                        return true;
                    }
                case HitStatus.Missed:
                    {
                        comboCount = 0;
                        break;
                    }
            }
            return false;
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
        public HitStatus TryHit(object input, in List<(ulong, object)> inputSnapshots);
        public HitStatus TryHit_InCombo(object input, in List<(ulong, object)> inputSnapshots);
    }
}
