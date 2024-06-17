﻿using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private List<Guid> _presetsByIndex;

        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;

            _dropdown.options.Clear();

            // Add the defaults
            _presetsByIndex = _tab.AddOptionsToDropdown(_dropdown);

            // Set index
            _dropdown.SetValueWithoutNotify(_presetsByIndex.IndexOf(_tab.SelectedPreset));
        }

        public void OnDropdownChange()
        {
            var preset = _presetsByIndex[_dropdown.value];

            _tab.SelectedPreset = preset;

            SettingsMenu.Instance.Refresh();
        }
    }
}