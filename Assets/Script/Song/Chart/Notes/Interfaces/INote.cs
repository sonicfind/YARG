using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Types;

namespace YARG.Song.Chart.Notes
{
    public interface INote
    {
        public bool HasActiveNotes();
        public ulong GetLongestSustain();
    }

    public class InputHandler
    {
        private List<(ulong, object)> inputs;
        private readonly object inputLock;

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

    public class Player
    {
        private readonly OverdrivePhrase[] overdrives;
        private int overdriveIndex = 0;
        private float overdrive = 0;
        private float overdriveOffset = 0;

        private readonly SoloPhrase[] soloes;
        private int soloIndex = 0;
        private float soloPercentage = 0;
        private bool soloActive = false;
        private bool soloBoxUpdated = false;

        private readonly (ulong, PlayableNote)[] notes;
        private (int, int) visibleNoteRange = new(0, 0);
        private (int, int) hittableNotes = new(0, 0);
        private readonly (ulong, ulong) hitWindow = new(55, 55);

        private readonly PlayableNote[] sustainedNotes;
        private readonly InputHandler inputHandler;
        private readonly List<(ulong, object)> inputSnapshots = new(2);
        private int comboCount = 0;
        private int notesHit = 0;

        public Player(OverdrivePhrase[] overdrives, SoloPhrase[] soloes, (ulong, PlayableNote)[] notes, InputHandler inputHandler, int numLanes)
        {
            this.overdrives = overdrives;
            this.soloes = soloes;
            this.notes = notes;
            this.inputHandler = inputHandler;
            sustainedNotes = new PlayableNote[numLanes];
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

        public void UpdateSolo(float percentage)
        {
            soloBoxUpdated = true;
            soloPercentage = percentage;
        }

        public void Update(ulong position)
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

            if (soloIndex < soloes.Length && soloes[soloIndex].start <= position)
            {
                if (!soloActive)
                    soloActive = true;
                else if (soloes[soloIndex].end <= position)
                {
                    soloActive = false;
                    while (true)
                    {
                        // TODO - Add solo score
                        soloIndex++;
                        if (soloIndex >= soloes.Length || position < soloes[soloIndex].start)
                        {
                            // Run "End of solo" action w/ result
                            int solo = soloIndex - 1;
                            break;
                        }

                        if (position < soloes[soloIndex].end)
                        {
                            soloActive = true;
                            break;
                        }
                    }
                }
            }

            if (overdriveIndex < overdrives.Length && position >= overdrives[overdriveIndex].start)


                overdrive += overdriveOffset;
            Math.Clamp(overdrive, 0, 1);
        }

        private bool HandleStatus(HitStatus status)
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

    public class OverdrivePhrase
    {
        public readonly ulong start;

        private readonly Player player;
        private readonly int numNotesInPhrase;
        private int numNotesHit;

        public bool ValidStatus { get; private set; } = true;

        public OverdrivePhrase(ulong start, Player player, int numNotesInPhrase)
        {
            this.start = start;
            this.player = player;
            this.numNotesInPhrase = numNotesInPhrase;
            numNotesHit = 0;
        }

        public void AddHits(int hits)
        {
            numNotesHit += hits;
            if (numNotesHit == numNotesInPhrase)
                player.AddOverdrive(.25f);
        }

        public void Invalidate()
        {
            ValidStatus = false;
            player.IncrementOverdrive();
        }
    }

    public class SoloPhrase
    {
        public readonly ulong start;
        public readonly ulong end;

        private readonly Player player;
        private readonly int numNotesInPhrase;
        private int numNotesHit;

        public SoloPhrase(ulong start, ulong end, Player player, int numNotesInPhrase)
        {
            this.player = player;
            this.start = start;
            this.end = end;
            this.numNotesInPhrase = numNotesInPhrase;
            numNotesHit = 0;
        }

        public void AddHits(int hits)
        {
            numNotesHit += hits;
            player.UpdateSolo(numNotesHit / (float) numNotesInPhrase);
        }
    }

    public class SubNote
    {
        public readonly int index;
        public readonly ulong endTick;
        public SubNote(int index, ulong endTick)
        {
            this.index = index;
            this.endTick = endTick;
        }
    }

#nullable enable
    public abstract class PlayableNote
    {
        protected readonly List<SubNote> lanes;
        protected OverdrivePhrase? overdrive;
        protected SoloPhrase? solo;

        protected PlayableNote(List<SubNote> lanes)
        {
            this.lanes = lanes;
        }

        public void AttachOverdrivePhrase(OverdrivePhrase phrase)
        {
            Debug.Assert(phrase != null);
            overdrive = phrase;
        }

        public void AttachSoloPhrase(SoloPhrase phrase)
        {
            Debug.Assert(phrase != null);
            solo = phrase;
        }

        public abstract HitStatus TryHit(object input, in List<(ulong, object)> inputSnapshots);
        public abstract HitStatus TryHit_InCombo(object input, in List<(ulong, object)> inputSnapshots);
    }
}
