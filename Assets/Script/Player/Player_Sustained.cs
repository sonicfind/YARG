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
    public class Player_Sustained<T> : Player_Instrument<T>
        where T : class, INote, new()
    {
        private bool useShrinkingHitWindow = true;

        private readonly List<Sustained_Playable> sustainedNotes = new();
        private readonly List<Sustained_Playable> pendingDrop = new();
        private readonly List<Sustained_Playable> droppedNotes = new();

        public Player_Sustained((GameObject, PlayerManager.Player) player, (FlatMapNode<long, T>[], int) notes, float[] notePositions) : base(player, notes, notePositions)
        {
        }

        public override void RunInput()
        {
            //for (int i = 0; i < inputs.Length; ++i)
            //{
            //    DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), inputs[i].position);
            //    ProcessOverdriveAndSustains(ref dual, ref inputs[i].input);
            //    ProcessInput(ref dual, ref inputs[i].input);
            //    HandleDrops(ref dual, ref inputs[i].input);
            //    CheckForOverhit(ref dual, ref inputs[i].input);
            //}

            var frame = currentInput;
            while (hittableQueue.Count > 0)
            {
                var dual = hittableQueue.Peek().position;
                if (dual.seconds > frame.position.seconds)
                    break;

                currentInput = new(dual, new());

                ProcessOverdriveAndSustains(dual);
                ProcessInput();
                CheckOverdriveActivity();
                HandleDrops();
                CheckForOverhit();
            }
            currentInput = frame;
        }

        public override void PostLoopCleanup(float position)
        {
            DequeueMissedHittables(position);
            {
                DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), position);
                ProcessOverdriveAndSustains(dual);
            }
            ProcessSoloes(position);
        }

        public override void Render(float position)
        {

        }

        private void ProcessOverdriveAndSustains(DualPosition position)
        {
            DrainOverdrive(position.ticks);
            AdjustSustains(ref position);
            ApplyOverdriveOffset();
        }

        protected override bool CheckOverdriveActivity()
        {
            if (overdriveActive)
                return true;

            if (overdrive < OVERDRIVE_THRESHOLD)
                return false;

            // TODO - check activation input
            if (true)
            {
                ActivateOverdrive(currentInput.position.ticks);
                return true;
            }
            return false;
        }

        private void AdjustSustains(ref DualPosition position)
        {
            for (int i = 0; i < sustainedNotes.Count;)
            {
                var note = sustainedNotes[i];
                var result = note.UpdateSustain(position, currentInput.input);

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

        private void HandleDrops()
        {
            for (int i = 0; i < pendingDrop.Count;)
            {
                var note = pendingDrop[i];
                var result = note.UpdateSustain(currentInput.position, currentInput.input);
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

            float endOfWindow = currentInput.position.seconds - visibilityRange.Item2;
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

        protected override bool HandleStatus(HitStatus status, PlayableNote hittable)
        {
            if (!base.HandleStatus(status, hittable))
                return false;

            if (status == HitStatus.Sustained)
            {
                sustainedNotes.Add(hittable as Sustained_Playable);
                Debug.Log("Test: Sustained added");
            }
            return true;
        }

        protected override void DequeueMissedHittables(float position)
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
                    droppedNotes.Add(hittable as Sustained_Playable);
                hittableQueue.Dequeue();
                ApplyMiss();
            }
        }
    }
}
