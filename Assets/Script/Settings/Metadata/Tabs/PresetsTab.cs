using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using Object = UnityEngine.Object;

namespace YARG.Settings.Metadata
{
    public enum PresetType
    {
        Camera,
        Colors,
        Engine,
    }

    public class PresetsTab : Tab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _presetTypeDropdown = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetTypeDropdown")
            .WaitForCompletion();
        private static readonly GameObject _presetDropdown = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetDropdown")
            .WaitForCompletion();
        private static readonly GameObject _presetActions = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetActions")
            .WaitForCompletion();
        private static readonly GameObject _presetDefaultText = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetDefaultText")
            .WaitForCompletion();

        private static readonly CameraPresetSubTab _cameraTab = new(CustomContentManager.CameraSettings, new TrackPreviewBuilder());
        private static readonly ColorProfileSubTab _colorsTab = new(CustomContentManager.ColorProfiles, new TrackPreviewBuilder());
        private static readonly EnginePresetSubTab _engineTab = new(CustomContentManager.EnginePresets, new TrackPreviewBuilder(forceShowHitWindow: true));

        private PresetType _currentPresetTab = PresetType.Camera;

        public void Rename()
        {
            if (IsCurrentPresetDefault())
            {
                return;
            }

            DialogManager.Instance.ShowRenameDialog("Rename Preset", value =>
            {
                switch (_currentPresetTab)
                {
                    case PresetType.Camera:
                        _cameraTab.RenamePreset(value);
                        break;
                    case PresetType.Colors:
                        _colorsTab.RenamePreset(value);
                        break;
                    case PresetType.Engine:
                        _engineTab.RenamePreset(value);
                        break;
                }
                SettingsMenu.Instance.Refresh();
            });
        }

        public void DeleteCurrentPreset()
        {
            if (IsCurrentPresetDefault())
            {
                return;
            }

            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    _cameraTab.DeletePreset();
                    _lastSelectedPresetOfType[0] = CameraPreset.Default.Id;
                    break;
                case PresetType.Colors:
                    _colorsTab.DeletePreset();
                    _lastSelectedPresetOfType[1] = ColorProfile.Default.Id;
                    break;
                case PresetType.Engine:
                    _engineTab.DeletePreset();
                    _lastSelectedPresetOfType[2] = EnginePreset.Default.Id;
                    break;
            }
            SettingsMenu.Instance.Refresh();
        }

        public void CopyCurrentPreset()
        {
            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    _lastSelectedPresetOfType[0] = _cameraTab.CopyPreset();
                    break;
                case PresetType.Colors:
                    _lastSelectedPresetOfType[1] = _colorsTab.CopyPreset();
                    break;
                case PresetType.Engine:
                    _lastSelectedPresetOfType[2] = _engineTab.CopyPreset();
                    break;
            }
            SettingsMenu.Instance.Refresh();
        }

        public void ImportPreset()
        {
            FileExplorerHelper.OpenChooseFile(null, "preset", path =>
            {
                switch (_currentPresetTab)
                {
                    case PresetType.Camera:
                        if (!_cameraTab.ImportPreset(path, out var id))
                        {
                            return;
                        }
                        _lastSelectedPresetOfType[0] = id;
                        break;
                    case PresetType.Colors:
                        if (!_cameraTab.ImportPreset(path, out id))
                        {
                            return;
                        }
                        _lastSelectedPresetOfType[1] = id;
                        break;
                    case PresetType.Engine:
                        if (!_cameraTab.ImportPreset(path, out id))
                        {
                            return;
                        }
                        _lastSelectedPresetOfType[2] = id;
                        break;
                }
                SettingsMenu.Instance.Refresh();
            });
        }

        public void ExportPreset()
        {
            var id = _lastSelectedPresetOfType[(int)_currentPresetTab];
            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    if (_cameraTab.CustomContent.TryGetCustomPreset(id, out var camera))
                    {
                        // Ask the user for an ending location
                        FileExplorerHelper.OpenSaveFile(null, camera.Name, "preset", camera.Export);
                    }
                    break;
                case PresetType.Colors:
                    if (_colorsTab.CustomContent.TryGetCustomPreset(id, out var colors))
                    {
                        // Ask the user for an ending location
                        FileExplorerHelper.OpenSaveFile(null, colors.Name, "preset", colors.Export);
                    }
                    break;
                case PresetType.Engine:
                    if (_engineTab.CustomContent.TryGetCustomPreset(id, out var engine))
                    {
                        // Ask the user for an ending location
                        FileExplorerHelper.OpenSaveFile(null, engine.Name, "preset", engine.Export);
                    }
                    break;
            }
        }

        private static readonly Guid[] _lastSelectedPresetOfType = { Guid.Empty, Guid.Empty, Guid.Empty };
        private static readonly List<string> _ignoredPathUpdates = new();

        public PresetType SelectedContent
        {
            get => _currentPresetTab;
            set
            {
                _currentPresetTab = value;
                ResetSelectedPreset();
            }
        }

        public Guid SelectedPreset
        {
            get => _lastSelectedPresetOfType[(int) _currentPresetTab];
            set => _lastSelectedPresetOfType[(int) _currentPresetTab] = value;
        }

        private FileSystemWatcher _watcher;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            ResetSelectedPreset();
        }

        public bool IsCurrentPresetDefault()
        {
            return _currentPresetTab switch
            {
                PresetType.Camera => CameraPreset.IsDefault(_cameraTab.Preset),
                PresetType.Colors => ColorProfile.IsDefault(_colorsTab.Preset),
                PresetType.Engine => EnginePreset.IsDefault(_engineTab.Preset),
                _ => true
            };
        }

        /// <summary>
        /// Adds all of the presets to the specified dropdown.
        /// </summary>
        /// <returns>
        /// A list containing all of the base presets in order as shown in the dropdown.
        /// </returns>
        public List<Guid> AddOptionsToDropdown(TMP_Dropdown dropdown)
        {
            return _currentPresetTab switch
            {
                PresetType.Camera => _cameraTab.CustomContent.AddOptionsToDropdown(dropdown),
                PresetType.Colors => _colorsTab.CustomContent.AddOptionsToDropdown(dropdown),
                PresetType.Engine => _engineTab.CustomContent.AddOptionsToDropdown(dropdown),
                _ => throw new NotImplementedException(),
            };
        }

        public override void OnTabEnter()
        {
            _ignoredPathUpdates.Clear();

            _watcher = new FileSystemWatcher(CustomContentManager.CustomizationDirectory, "*.json")
            {
                EnableRaisingEvents = true,
            };

            // This is async, so we must queue an action for the main thread
            _watcher.Changed += (_, args) =>
            {
                YargLogger.LogDebug("Preset change detected!");

                // Queue the reload on the main thread
                UnityMainThreadCallback.QueueEvent(() =>
                {
                    OnPresetChanged(args.FullPath);
                });
            };
        }

        private void OnPresetChanged(string path)
        {
            if (_ignoredPathUpdates.Contains(path))
            {
                YargLogger.LogDebug("Ignored preset change.");
                _ignoredPathUpdates.Remove(path);
                return;
            }

            switch(Path.GetFileName(path))
            {
                case CustomContentManager.COLORS_FILE:
                    CustomContentManager.ColorProfiles.LoadPresetsFromFile();
                    break;
                case CustomContentManager.CAMERAS_FILE:
                    CustomContentManager.CameraSettings.LoadPresetsFromFile();
                    break;
                case CustomContentManager.THEMES_FILE:
                    CustomContentManager.ThemePresets.LoadPresetsFromFile();
                    break;
                case CustomContentManager.ENGINES_FILE:
                    CustomContentManager.EnginePresets.LoadPresetsFromFile();
                    break;
                default:
                    return;
            }

            if (SettingsMenu.Instance.gameObject.activeSelf && SettingsMenu.Instance.CurrentTab is PresetsTab)
            {
                SettingsMenu.Instance.Refresh();
            }
        }

        public override void OnTabExit()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            // Create the preset type dropdown
            var typeDropdown = Object.Instantiate(_presetTypeDropdown, settingContainer);
            typeDropdown.GetComponent<PresetTypeDropdown>().Initialize(this);

            // Create the preset dropdown
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetDropdown>().Initialize(this);

            // Create the preset actions
            var actions = Object.Instantiate(_presetActions, settingContainer);
            actions.GetComponent<PresetActions>().Initialize(this);

            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    if (_cameraTab.CustomContent.TryGetCustomPreset(SelectedPreset, out var camera))
                    {
                        _cameraTab.Preset = camera;
                        _cameraTab.BuildSettingTab(settingContainer, navGroup);
                        return;
                    }
                    break;
                case PresetType.Colors:
                    if (_colorsTab.CustomContent.TryGetCustomPreset(SelectedPreset, out var colors))
                    {
                        _colorsTab.Preset = colors;
                        _colorsTab.BuildSettingTab(settingContainer, navGroup);
                        return;
                    }
                    break;
                case PresetType.Engine:
                    if (_engineTab.CustomContent.TryGetCustomPreset(SelectedPreset, out var engine))
                    {
                        _engineTab.Preset = engine;
                        _engineTab.BuildSettingTab(settingContainer, navGroup);
                        return;
                    }
                    break;
            }
            // Only reached as a last resort
            Object.Instantiate(_presetDefaultText, settingContainer);
        }

        public override async UniTask BuildPreviewWorld(Transform worldContainer)
        {
            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    await _cameraTab.BuildPreviewWorld(worldContainer);
                    break;
                case PresetType.Colors:
                    await _colorsTab.BuildPreviewWorld(worldContainer);
                    break;
                case PresetType.Engine:
                    await _engineTab.BuildPreviewWorld(worldContainer);
                    break;
            }
        }

        public override async UniTask BuildPreviewUI(Transform uiContainer)
        {
            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    await _cameraTab.BuildPreviewUI(uiContainer);
                    break;
                case PresetType.Colors:
                    await _colorsTab.BuildPreviewUI(uiContainer);
                    break;
                case PresetType.Engine:
                    await _engineTab.BuildPreviewUI(uiContainer);
                    break;
            }
        }

        public override void OnSettingChanged()
        {
            switch (_currentPresetTab)
            {
                case PresetType.Camera:
                    _cameraTab.OnSettingChanged();
                    break;
                case PresetType.Colors:
                    _colorsTab.OnSettingChanged();
                    break;
                case PresetType.Engine:
                    _engineTab.OnSettingChanged();
                    break;
            }
        }

        private void ResetSelectedPreset()
        {
            if (!_cameraTab.CustomContent.HasPreset(_lastSelectedPresetOfType[0]))
            {
                _lastSelectedPresetOfType[0] = CameraPreset.Default.Id;
                _cameraTab.Preset = CameraPreset.Default;
            }

            if (!_colorsTab.CustomContent.HasPreset(_lastSelectedPresetOfType[1]))
            {
                _lastSelectedPresetOfType[1] = ColorProfile.Default.Id;
                _colorsTab.Preset = ColorProfile.Default;
            }

            if (!_engineTab.CustomContent.HasPreset(_lastSelectedPresetOfType[2]))
            {
                _lastSelectedPresetOfType[2] = EnginePreset.Default.Id;
                _engineTab.Preset = EnginePreset.Default;
            }
        }

        private static PresetContainer<TPreset> GetLastSelectedPreset<TPreset>(ref Guid id, PresetSubTab<TPreset> tab)
            where TPreset : struct
        {
            if (!tab.CustomContent.TryGetPreset(id, out var preset))
            {
                tab.Preset = preset = tab.CustomContent[0];
                id = preset.Id;
            }
            return preset;
        }

        public static CameraPreset GetLastSelectedCameraPresetConfig()
        {
            return GetLastSelectedPreset(ref _lastSelectedPresetOfType[0], _cameraTab).Config;
        }

        public static ColorProfile GetLastSelectedColorProfileConfig()
        {
            return GetLastSelectedPreset(ref _lastSelectedPresetOfType[1], _colorsTab).Config;
        }

        public static EnginePreset GetLastSelectedEnginePresetConfig()
        {
            return GetLastSelectedPreset(ref _lastSelectedPresetOfType[2], _engineTab).Config;
        }

        public static void IgnorePathUpdate(string path)
        {
            _ignoredPathUpdates.Add(path);
        }
    }
}