using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Menu.Persistent;
using YARG.Settings.Metadata;

namespace YARG.Settings.Customization
{
    public class CustomContentContainer<TPreset>
        where TPreset : unmanaged
    {
        public IReadOnlyList<PresetContainer<TPreset>> DefaultPresets => _defaultPresets;
        public IReadOnlyList<PresetContainer<TPreset>> CustomPresets => _customPresets;

        private readonly string _oldDirectory;
        private readonly string _presetsFile;

        protected readonly PresetContainer<TPreset>[] _defaultPresets;
        protected List<PresetContainer<TPreset>> _customPresets = new();

        public CustomContentContainer(PresetContainer<TPreset>[] defaults, string oldDirectory, string presetsFile)
        {
            _defaultPresets = defaults;
            _oldDirectory = oldDirectory;
            _presetsFile = presetsFile;
            //_directory = Path.Combine(CustomContentManager.CustomizationDirectory, directory);
            //Directory.CreateDirectory(_directory);
            //LoadFiles();
        }

        public PresetContainer<TPreset> this[int index]
        {
            get
            {
                for (int i = 0; i < _defaultPresets.Length; ++i)
                {
                    if (i == index)
                    {
                        return _defaultPresets[i];
                    }
                }

                index -= _defaultPresets.Length;
                for (int i = 0; i < _customPresets.Count; ++i)
                {
                    if (i == index)
                    {
                        return _customPresets[i];
                    }
                }
                throw new IndexOutOfRangeException();
            }
        }

        public bool AddPreset(in PresetContainer<TPreset> preset)
        {
            if (HasPreset(preset.Id))
            {
                return false;
            }
            _customPresets.Add(preset);
            return true;
        }

        public bool SetPreset(in PresetContainer<TPreset> preset)
        {
            for (int i = 0; i < _customPresets.Count; ++i)
            {
                if (_customPresets[i].Id == preset.Id)
                {
                    _customPresets[i] = preset;
                    return true;
                }
            }
            return false;
        }

        public bool DeletePreset(Guid id)
        {
            for (int i = 0; i < _customPresets.Count; ++i)
            {
                if (CustomPresets[i].Id == id)
                {
                    _customPresets.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool HasPreset(Guid id)
        {
            foreach (var preset in DefaultPresets)
            {
                if (preset.Id == id) return true;
            }

            foreach (var preset in _customPresets)
            {
                if (preset.Id == id) return true;
            }
            return false;
        }

        public bool TryGetPreset(Guid id, out PresetContainer<TPreset> preset)
        {
            foreach (var def in DefaultPresets)
            {
                if (def.Id == id)
                {
                    preset = def;
                    return true;
                }
            }

            return TryGetCustomPreset(id, out preset);
        }

        public bool TryGetCustomPreset(Guid id, out PresetContainer<TPreset> preset)
        {
            foreach (var cus in _customPresets)
            {
                if (cus.Id == id)
                {
                    preset = cus;
                    return true;
                }
            }
            preset = _defaultPresets[0];
            return false;
        }

        /// <summary>
        /// Adds all of the presets to the specified dropdown.
        /// </summary>
        /// <returns>
        /// A list containing all of the base presets in order as shown in the dropdown.
        /// </returns>
        public List<Guid> AddOptionsToDropdown(TMP_Dropdown dropdown)
        {
            var list = new List<Guid>();

            dropdown.options.Clear();

            // Add defaults
            foreach (var preset in _defaultPresets)
            {
                dropdown.options.Add(new($"<color=#1CCFFF>{preset.Name}</color>"));
                list.Add(preset.Id);
            }

            // Add customs
            foreach (var preset in _customPresets)
            {
                dropdown.options.Add(new(preset.Name));
                list.Add(preset.Id);
            }

            return list;
        }

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            Converters = new List<JsonConverter>
            {
                new JsonColorConverter(),
            }
        };

        public void SavePresetsToFile()
        {
            var path = Path.Combine(CustomContentManager.CustomizationDirectory, _presetsFile);
            if (_customPresets.Count > 0)
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(_customPresets, JsonSettings));
            }
            else
            {
                File.Delete(path);
            }
        }

        public void LoadPresetsFromFile()
        {
            var path = Path.Combine(CustomContentManager.CustomizationDirectory, _presetsFile);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                _customPresets = JsonConvert.DeserializeObject<List<PresetContainer<TPreset>>>(json, JsonSettings);
            }
        }

        //private void LoadFiles()
        //{
        //    _customPresets.Clear();

        //    var renameList = new List<(string From, string To)>();

        //    PathHelper.SafeEnumerateFiles(_directory, "*.json", true, (path) =>
        //    {
        //        var preset = JsonConvert.DeserializeObject<TPreset>(File.ReadAllText(path), JsonSettings);

        //        // If the path is incorrect, rename it
        //        var correctPath = GetFileNameForPreset(preset);
        //        if (Path.GetFileName(path) != correctPath)
        //        {
        //            // We must do this after since we are in the middle of enumerating it
        //            var correctFullPath = Path.Join(_directory, correctPath);
        //            renameList.Add((path, correctFullPath));
        //        }

        //        if (!_customPresets.TryAdd(preset.Id, preset))
        //        {
        //            YargLogger.LogFormatWarning("Duplicate preset `{0}` found!", path);
        //        }
        //        return true;
        //    });

        //    // Rename all files
        //    foreach (var (from, to) in renameList)
        //    {
        //        try
        //        {
        //            if (!File.Exists(to))
        //            {
        //                // If the file doesn't exist, just rename it
        //                File.Move(from, to);
        //                YargLogger.LogFormatInfo("Renamed preset file from `{0}` to its correct form.", from);
        //            }
        //            else
        //            {
        //                // If it does, delete the original file (since it's probably a duplicate)
        //                File.Delete(from);
        //                YargLogger.LogFormatInfo("Deleted duplicate preset file `{0}`.", from);
        //            }
        //        }
        //        catch (Exception e)
        //        {
        //            YargLogger.LogException(e, $"Failed to move file `{from}`.");
        //        }
        //    }
        //}

        private static string GetFileNameForPreset(in PresetContainer<TPreset> preset)
        {
            // Limit the file name to 20 characters
            string fileName = preset.Name;
            if (fileName.Length > 20)
            {
                fileName = fileName[..20];
            }

            // Remove symbols
            fileName = PathHelper.SanitizeFileName(fileName);

            // Add the end
            fileName += $".{preset.Id.ToString()[..8]}.json";

            return fileName;
        }

        protected virtual void AddAdditionalFilesToExport(ZipArchive archive)
        {
        }

        protected virtual void SaveAdditionalFilesFromExport(ZipArchive archive, TPreset preset)
        {
        }
    }
}