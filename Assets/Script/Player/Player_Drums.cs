using ManagedBass;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using YARG.Assets.Script.Types;
using YARG.Player;
using YARG.Song.Chart.Notes;
using YARG.Types;

namespace YARG.Player
{
    public class Player_Drums<T> : Player_Instrument<T>
        where T : DrumNote, new()
    {
        private List<OverdriveActivationPhrase> activations = new();

        public Player_Drums((GameObject, PlayerManager.Player) player, (FlatMapNode<long, T>[], int) notes, float[] notePositions) : base(player, notes, notePositions)
        {
        }

        public override void AttachPhrase(PlayablePhrase phrase)
        {
            if (phrase is OverdriveActivationPhrase activation)
                activations.Add(activation);
            else
                base.AttachPhrase(phrase);
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

            while (hittableQueue.Count > 0)
            {
                var dual = hittableQueue.Peek().position;
                if (dual.seconds > currentInput.position.seconds)
                    break;

                object input = new();
                ProcessOverdrive(dual.ticks);
                ProcessInput();
                CheckForOverhit();
            }
        }

        public override void PostLoopCleanup(float position)
        {
            DualPosition dual = new(sync.ConvertToTicks(position, ref tempoIndex), position);
            DequeueMissedHittables(position);
            ProcessOverdrive(dual.ticks);
            ProcessSoloes(position);
        }

        protected override bool CheckOverdriveActivity()
        {
            return overdriveActive;
        }

        public override void Render(float position)
        {

        }

        private void ProcessOverdrive(long tickPosition)
        {
            DrainOverdrive(tickPosition);
            ApplyOverdriveOffset();
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
                    return true;
                default:
                    return false;
            }
        }

        protected override void DequeueMissedHittables(float position)
        {
            float endOfWindow = position - hitWindow.Item2;
            while (hittableQueue.Count > 0)
            {
                var hittable = hittableQueue.Peek();
                if (comboCount > 0)
                {
                    int nextIndex = nextHittable - hittableQueue.Count + 1;
                    if (nextIndex == notePositions.Length || position < notePositions[nextIndex])
                        break;
                }
                else if (hittable.position.seconds >= endOfWindow)
                    break;

                hittableQueue.Dequeue();
                ApplyMiss();
            }
        }
    }
}
