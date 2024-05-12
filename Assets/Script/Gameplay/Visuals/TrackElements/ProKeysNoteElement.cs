﻿using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class ProKeysNoteElement : NoteElement<ProKeysNote, ProKeysPlayer>
    {
        [Space]
        [SerializeField]
        private SustainLine _sustainLine;

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays(1);
            AssignNoteGroup(models, starPowerModels, 0, ThemeNoteType.Normal);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            // Set the position
            transform.localPosition = new Vector3(Player.GetNoteX(NoteRef.Key), 0f, 0f);

            NoteGroup = noteGroups[0];
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            // Set line length
            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);
            }

            // Set note and sustain color
            UpdateColor();
        }

        public override void HitNote()
        {
            base.HitNote();

            if (NoteRef.IsSustain)
            {
                HideNotes();
            }
            else
            {
                ParentPool.Return(this);
            }
        }

        protected override void UpdateElement()
        {
            base.UpdateElement();

            UpdateSustain();
        }

        protected override void OnNoteStateChanged()
        {
            base.OnNoteStateChanged();

            UpdateColor();
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        private void UpdateSustain()
        {
            _sustainLine.UpdateSustainLine(Player.NoteSpeed * GameManager.SongSpeed);
        }

        private void UpdateColor()
        {
            // var colors = Player.Player.ColorProfile.FiveFretGuitar;

            // Get which note color to use
            // var colorNoStarPower = colors.GetNoteColor(NoteRef.Fret);
            // var color = NoteRef.IsStarPower
            //     ? colors.GetNoteStarPowerColor(NoteRef.Fret)
            //     : colorNoStarPower;

            // Set the note color
            NoteGroup.SetColorWithEmission(Color.white, Color.white);

            // The rest of this method is for sustain only
            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, Color.white);
        }

        protected override void HideElement()
        {
            HideNotes();

            _sustainLine.gameObject.SetActive(false);
        }
    }
}