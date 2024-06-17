using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Menu.Settings
{
    public class PresetTypeDropdown : MonoBehaviour
    {
        [SerializeField]
        private TMP_Dropdown _dropdown;

        private PresetsTab _tab;

        public void Initialize(PresetsTab tab)
        {
            _tab = tab;

            // Add the options (in order)
            _dropdown.options = new List<TMP_Dropdown.OptionData>()
            {
                new(LocaleHelper.LocalizeString("Settings", $"PresetType.{PresetType.Camera}")),
                new(LocaleHelper.LocalizeString("Settings", $"PresetType.{PresetType.Colors}")),
                new(LocaleHelper.LocalizeString("Settings", $"PresetType.{PresetType.Engine}"))
            };

            // Set index
            _dropdown.SetValueWithoutNotify((int) tab.SelectedContent);
        }

        public void OnDropdownChange()
        {
            _tab.SelectedContent = (PresetType)_dropdown.value;
            SettingsMenu.Instance.Refresh();
        }
    }
}