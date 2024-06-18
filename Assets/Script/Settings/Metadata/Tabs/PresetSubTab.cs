using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Components;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    public abstract class PresetSubTab<TPreset> : Tab
        where TPreset : struct
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _headerPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Header")
            .WaitForCompletion();

        protected readonly Dictionary<string, ISettingType> _settingFields = new();
        public readonly CustomContentContainer<TPreset> CustomContent;

        protected PresetSubTab(CustomContentContainer<TPreset> customContent, in PresetContainer<TPreset> preset, IPreviewBuilder previewBuilder = null)
            : base("Presets", "Generic", previewBuilder)
        {
            CustomContent = customContent;
            _preset = preset;
        }

        protected PresetContainer<TPreset> _preset;

        public abstract PresetContainer<TPreset> Preset { get; set; }

        public void RenamePreset(string name)
        {
            _preset.Name = name;
            SettingsMenu.Instance.OnSettingChanged();
        }

        public bool ImportPreset(string path, out System.Guid id)
        {
            var preset = PresetContainer<TPreset>.Import(path);
            if (!CustomContent.AddPreset(in _preset))
            {
                id = System.Guid.Empty;
                return false;
            }
            _preset = preset;
            SettingsMenu.Instance.OnSettingChanged();
            id = preset.Id;
            return true;
        }

        public void DeletePreset()
        {
            CustomContent.DeletePreset(_preset.Id);
            _preset = CustomContent.DefaultPresets[0];
        }

        public System.Guid CopyPreset()
        {
            _preset.Name = $"Copy of {_preset.Name}";
            do
            {
                _preset.Id = System.Guid.NewGuid();
            } while(!CustomContent.AddPreset(in _preset));
            SettingsMenu.Instance.OnSettingChanged();
            return _preset.Id;
        }

        protected static void SpawnHeader(Transform container, string unlocalizedText)
        {
            // Spawn in the header
            var go = Object.Instantiate(_headerPrefab, container);

            // Set header text
            go.GetComponentInChildren<LocalizeStringEvent>().StringReference =
                LocaleHelper.StringReference("Settings", $"Header.{unlocalizedText}");
        }

        protected void CreateFields<TSetting>(Transform container, NavigationGroup navGroup, string presetName, Dictionary<string, TSetting> settings)
            where TSetting : ISettingType
        {
            foreach ((string name, var setting) in settings)
            {
                CreateField(container, navGroup, presetName, name, setting);
            }
        }

        protected void CreateField<TSetting>(Transform container, NavigationGroup navGroup, string presetName, string name, TSetting settingType)
            where TSetting : ISettingType
        {
            var visual = SpawnSettingVisual(settingType, container);

            visual.AssignPresetSetting($"{presetName}.{name}", settingType);
            _settingFields.Add(name, settingType);
            navGroup.AddNavigatable(visual.gameObject);
        }
    }

    public sealed class CameraPresetSubTab : PresetSubTab<CameraPreset>
    {
        private readonly Dictionary<string, SliderSetting> _settings = new();

        public CameraPresetSubTab(CustomContentContainer<CameraPreset> customContent, IPreviewBuilder previewBuilder = null)
            : base(customContent, CameraPreset.Default, previewBuilder)
        {
            _settings = new()
            {
                {nameof(CameraPreset.FieldOfView), new SliderSetting(_preset.Config.FieldOfView, 40f, 150f, val => _preset.Config.FieldOfView = val) },
                {nameof(CameraPreset.PositionY),   new SliderSetting(_preset.Config.PositionY,   0f,  4f,   val => _preset.Config.PositionY = val) },
                {nameof(CameraPreset.PositionZ),   new SliderSetting(_preset.Config.PositionZ,   0f,  12f,  val => _preset.Config.PositionZ = val) },
                {nameof(CameraPreset.Rotation),    new SliderSetting(_preset.Config.Rotation,    0f,  180f, val => _preset.Config.Rotation = val) },
                {nameof(CameraPreset.FadeLength),  new SliderSetting(_preset.Config.FadeLength,  0f,  5f,   val => _preset.Config.FadeLength = val) },
                {nameof(CameraPreset.CurveFactor), new SliderSetting(_preset.Config.CurveFactor, -3f, 3f,   val => _preset.Config.CurveFactor = val) },
            };
        }

        public override PresetContainer<CameraPreset> Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                _settings[nameof(CameraPreset.FieldOfView)].SetValueWithoutNotify(_preset.Config.FieldOfView);
                _settings[nameof(CameraPreset.PositionY)].SetValueWithoutNotify(_preset.Config.PositionY);
                _settings[nameof(CameraPreset.PositionZ)].SetValueWithoutNotify(_preset.Config.PositionZ);
                _settings[nameof(CameraPreset.Rotation)].SetValueWithoutNotify(_preset.Config.Rotation);
                _settings[nameof(CameraPreset.FadeLength)].SetValueWithoutNotify(_preset.Config.FadeLength);
                _settings[nameof(CameraPreset.CurveFactor)].SetValueWithoutNotify(_preset.Config.CurveFactor);
                SettingsMenu.Instance.OnSettingChanged();
            }
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            _settingFields.Clear();
            SpawnHeader(settingContainer, "PresetSettings");
            CreateFields(settingContainer, navGroup, nameof(CameraPreset), _settings);
        }
    }

    public sealed class ColorProfileSubTab : PresetSubTab<ColorProfile>
    {
        private GameMode _mode = GameMode.FiveFretGuitar;
        private readonly DropdownSetting<GameMode> _modeDropdown;
        private readonly Dictionary<GameMode, Dictionary<string, ColorSetting>> _allSettings = new();

        public ColorProfileSubTab(CustomContentContainer<ColorProfile> customContent, IPreviewBuilder previewBuilder = null)
            : base(customContent, ColorProfile.Default, previewBuilder)
        {
            _modeDropdown = new DropdownSetting<GameMode>(GameMode.FiveFretGuitar, RefreshForSubSection)
            {
                GameMode.FiveFretGuitar,
                GameMode.FourLaneDrums,
                GameMode.FiveLaneDrums,
            };

            // Set the preview type
            if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
            {
                // Yucky.
                // TODO: Redo this whole system!
                trackPreviewBuilder.StartingGameMode = GameMode.FiveFretGuitar;
            }
            else
            {
                YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
            }

            _allSettings[GameMode.FiveFretGuitar] = new Dictionary<string, ColorSetting>()
            {
                { nameof(ColorProfile.FiveFretGuitarColors.OpenFret),            new ColorSetting(_preset.Config.FiveFretGuitar.OpenFret.ToUnityColor(),            true, color => _preset.Config.FiveFretGuitar.OpenFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.GreenFret),           new ColorSetting(_preset.Config.FiveFretGuitar.GreenFret.ToUnityColor(),           true, color => _preset.Config.FiveFretGuitar.GreenFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.RedFret),             new ColorSetting(_preset.Config.FiveFretGuitar.RedFret.ToUnityColor(),             true, color => _preset.Config.FiveFretGuitar.RedFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.YellowFret),          new ColorSetting(_preset.Config.FiveFretGuitar.YellowFret.ToUnityColor(),          true, color => _preset.Config.FiveFretGuitar.YellowFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.BlueFret),            new ColorSetting(_preset.Config.FiveFretGuitar.BlueFret.ToUnityColor(),            true, color => _preset.Config.FiveFretGuitar.BlueFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OrangeFret),          new ColorSetting(_preset.Config.FiveFretGuitar.OrangeFret.ToUnityColor(),          true, color => _preset.Config.FiveFretGuitar.OrangeFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OpenFretInner),       new ColorSetting(_preset.Config.FiveFretGuitar.OpenFretInner.ToUnityColor(),       true, color => _preset.Config.FiveFretGuitar.OpenFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.GreenFretInner),      new ColorSetting(_preset.Config.FiveFretGuitar.GreenFretInner.ToUnityColor(),      true, color => _preset.Config.FiveFretGuitar.GreenFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.RedFretInner),        new ColorSetting(_preset.Config.FiveFretGuitar.RedFretInner.ToUnityColor(),        true, color => _preset.Config.FiveFretGuitar.RedFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.YellowFretInner),     new ColorSetting(_preset.Config.FiveFretGuitar.YellowFretInner.ToUnityColor(),     true, color => _preset.Config.FiveFretGuitar.YellowFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.BlueFretInner),       new ColorSetting(_preset.Config.FiveFretGuitar.BlueFretInner.ToUnityColor(),       true, color => _preset.Config.FiveFretGuitar.BlueFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OrangeFretInner),     new ColorSetting(_preset.Config.FiveFretGuitar.OrangeFretInner.ToUnityColor(),     true, color => _preset.Config.FiveFretGuitar.OrangeFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OpenParticles),       new ColorSetting(_preset.Config.FiveFretGuitar.OpenParticles.ToUnityColor(),       true, color => _preset.Config.FiveFretGuitar.OpenParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.GreenParticles),      new ColorSetting(_preset.Config.FiveFretGuitar.GreenParticles.ToUnityColor(),      true, color => _preset.Config.FiveFretGuitar.GreenParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.RedParticles),        new ColorSetting(_preset.Config.FiveFretGuitar.RedParticles.ToUnityColor(),        true, color => _preset.Config.FiveFretGuitar.RedParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.YellowParticles),     new ColorSetting(_preset.Config.FiveFretGuitar.YellowParticles.ToUnityColor(),     true, color => _preset.Config.FiveFretGuitar.YellowParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.BlueParticles),       new ColorSetting(_preset.Config.FiveFretGuitar.BlueParticles.ToUnityColor(),       true, color => _preset.Config.FiveFretGuitar.BlueParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OrangeParticles),     new ColorSetting(_preset.Config.FiveFretGuitar.OrangeParticles.ToUnityColor(),     true, color => _preset.Config.FiveFretGuitar.OrangeParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OpenNote),            new ColorSetting(_preset.Config.FiveFretGuitar.OpenNote.ToUnityColor(),            true, color => _preset.Config.FiveFretGuitar.OpenNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.GreenNote),           new ColorSetting(_preset.Config.FiveFretGuitar.GreenNote.ToUnityColor(),           true, color => _preset.Config.FiveFretGuitar.GreenNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.RedNote),             new ColorSetting(_preset.Config.FiveFretGuitar.RedNote.ToUnityColor(),             true, color => _preset.Config.FiveFretGuitar.RedNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.YellowNote),          new ColorSetting(_preset.Config.FiveFretGuitar.YellowNote.ToUnityColor(),          true, color => _preset.Config.FiveFretGuitar.YellowNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.BlueNote),            new ColorSetting(_preset.Config.FiveFretGuitar.BlueNote.ToUnityColor(),            true, color => _preset.Config.FiveFretGuitar.BlueNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OrangeNote),          new ColorSetting(_preset.Config.FiveFretGuitar.OrangeNote.ToUnityColor(),          true, color => _preset.Config.FiveFretGuitar.OrangeNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OpenNoteStarPower),   new ColorSetting(_preset.Config.FiveFretGuitar.OpenNoteStarPower.ToUnityColor(),   true, color => _preset.Config.FiveFretGuitar.OpenNoteStarPower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.GreenNoteStarPower),  new ColorSetting(_preset.Config.FiveFretGuitar.GreenNoteStarPower.ToUnityColor(),  true, color => _preset.Config.FiveFretGuitar.GreenNoteStarPower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.RedNoteStarPower),    new ColorSetting(_preset.Config.FiveFretGuitar.RedNoteStarPower.ToUnityColor(),    true, color => _preset.Config.FiveFretGuitar.RedNoteStarPower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.YellowNoteStarPower), new ColorSetting(_preset.Config.FiveFretGuitar.YellowNoteStarPower.ToUnityColor(), true, color => _preset.Config.FiveFretGuitar.YellowNoteStarPower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.BlueNoteStarPower),   new ColorSetting(_preset.Config.FiveFretGuitar.BlueNoteStarPower.ToUnityColor(),   true, color => _preset.Config.FiveFretGuitar.BlueNoteStarPower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveFretGuitarColors.OrangeNoteStarPower), new ColorSetting(_preset.Config.FiveFretGuitar.OrangeNoteStarPower.ToUnityColor(), true, color => _preset.Config.FiveFretGuitar.OrangeNoteStarPower = color.ToSystemColor())},
            };

            _allSettings[GameMode.FourLaneDrums] = new Dictionary<string, ColorSetting>()
            {
                { nameof(ColorProfile.FourLaneDrumsColors.KickFret             ), new ColorSetting(_preset.Config.FourLaneDrums.KickFret.ToUnityColor(),              true, color => _preset.Config.FourLaneDrums.KickFret = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedFret              ), new ColorSetting(_preset.Config.FourLaneDrums.RedFret.ToUnityColor(),               true, color => _preset.Config.FourLaneDrums.RedFret = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowFret           ), new ColorSetting(_preset.Config.FourLaneDrums.YellowFret.ToUnityColor(),            true, color => _preset.Config.FourLaneDrums.YellowFret = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueFret             ), new ColorSetting(_preset.Config.FourLaneDrums.BlueFret.ToUnityColor(),              true, color => _preset.Config.FourLaneDrums.BlueFret = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenFret            ), new ColorSetting(_preset.Config.FourLaneDrums.GreenFret.ToUnityColor(),             true, color => _preset.Config.FourLaneDrums.GreenFret = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.KickFretInner        ), new ColorSetting(_preset.Config.FourLaneDrums.KickFretInner.ToUnityColor(),         true, color => _preset.Config.FourLaneDrums.KickFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedFretInner         ), new ColorSetting(_preset.Config.FourLaneDrums.RedFretInner.ToUnityColor(),          true, color => _preset.Config.FourLaneDrums.RedFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowFretInner      ), new ColorSetting(_preset.Config.FourLaneDrums.YellowFretInner.ToUnityColor(),       true, color => _preset.Config.FourLaneDrums.YellowFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueFretInner        ), new ColorSetting(_preset.Config.FourLaneDrums.BlueFretInner.ToUnityColor(),         true, color => _preset.Config.FourLaneDrums.BlueFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenFretInner       ), new ColorSetting(_preset.Config.FourLaneDrums.GreenFretInner.ToUnityColor(),        true, color => _preset.Config.FourLaneDrums.GreenFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.KickParticles        ), new ColorSetting(_preset.Config.FourLaneDrums.KickParticles.ToUnityColor(),         true, color => _preset.Config.FourLaneDrums.KickParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedParticles         ), new ColorSetting(_preset.Config.FourLaneDrums.RedParticles.ToUnityColor(),          true, color => _preset.Config.FourLaneDrums.RedParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowParticles      ), new ColorSetting(_preset.Config.FourLaneDrums.YellowParticles.ToUnityColor(),       true, color => _preset.Config.FourLaneDrums.YellowParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueParticles        ), new ColorSetting(_preset.Config.FourLaneDrums.BlueParticles.ToUnityColor(),         true, color => _preset.Config.FourLaneDrums.BlueParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenParticles       ), new ColorSetting(_preset.Config.FourLaneDrums.GreenParticles.ToUnityColor(),        true, color => _preset.Config.FourLaneDrums.GreenParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.KickNote             ), new ColorSetting(_preset.Config.FourLaneDrums.KickNote.ToUnityColor(),              true, color => _preset.Config.FourLaneDrums.KickNote = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedDrum              ), new ColorSetting(_preset.Config.FourLaneDrums.RedDrum.ToUnityColor(),               true, color => _preset.Config.FourLaneDrums.RedDrum = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowDrum           ), new ColorSetting(_preset.Config.FourLaneDrums.YellowDrum.ToUnityColor(),            true, color => _preset.Config.FourLaneDrums.YellowDrum = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueDrum             ), new ColorSetting(_preset.Config.FourLaneDrums.BlueDrum.ToUnityColor(),              true, color => _preset.Config.FourLaneDrums.BlueDrum = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenDrum            ), new ColorSetting(_preset.Config.FourLaneDrums.GreenDrum.ToUnityColor(),             true, color => _preset.Config.FourLaneDrums.GreenDrum = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedCymbal            ), new ColorSetting(_preset.Config.FourLaneDrums.RedCymbal.ToUnityColor(),             true, color => _preset.Config.FourLaneDrums.RedCymbal = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowCymbal         ), new ColorSetting(_preset.Config.FourLaneDrums.YellowCymbal.ToUnityColor(),          true, color => _preset.Config.FourLaneDrums.YellowCymbal = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueCymbal           ), new ColorSetting(_preset.Config.FourLaneDrums.BlueCymbal.ToUnityColor(),            true, color => _preset.Config.FourLaneDrums.BlueCymbal = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenCymbal          ), new ColorSetting(_preset.Config.FourLaneDrums.GreenCymbal.ToUnityColor(),           true, color => _preset.Config.FourLaneDrums.GreenCymbal = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.KickStarpower        ), new ColorSetting(_preset.Config.FourLaneDrums.KickStarpower.ToUnityColor(),         true, color => _preset.Config.FourLaneDrums.KickStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedDrumStarpower     ), new ColorSetting(_preset.Config.FourLaneDrums.RedDrumStarpower.ToUnityColor(),      true, color => _preset.Config.FourLaneDrums.RedDrumStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowDrumStarpower  ), new ColorSetting(_preset.Config.FourLaneDrums.YellowDrumStarpower.ToUnityColor(),   true, color => _preset.Config.FourLaneDrums.YellowDrumStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueDrumStarpower    ), new ColorSetting(_preset.Config.FourLaneDrums.BlueDrumStarpower.ToUnityColor(),     true, color => _preset.Config.FourLaneDrums.BlueDrumStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenDrumStarpower   ), new ColorSetting(_preset.Config.FourLaneDrums.GreenDrumStarpower.ToUnityColor(),    true, color => _preset.Config.FourLaneDrums.GreenDrumStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.RedCymbalStarpower   ), new ColorSetting(_preset.Config.FourLaneDrums.RedCymbalStarpower.ToUnityColor(),    true, color => _preset.Config.FourLaneDrums.RedCymbalStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.YellowCymbalStarpower), new ColorSetting(_preset.Config.FourLaneDrums.YellowCymbalStarpower.ToUnityColor(), true, color => _preset.Config.FourLaneDrums.YellowCymbalStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.BlueCymbalStarpower  ), new ColorSetting(_preset.Config.FourLaneDrums.BlueCymbalStarpower.ToUnityColor(),   true, color => _preset.Config.FourLaneDrums.BlueCymbalStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.GreenCymbalStarpower ), new ColorSetting(_preset.Config.FourLaneDrums.GreenCymbalStarpower.ToUnityColor(),  true, color => _preset.Config.FourLaneDrums.GreenCymbalStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FourLaneDrumsColors.ActivationNote       ), new ColorSetting(_preset.Config.FourLaneDrums.ActivationNote.ToUnityColor(),        true, color => _preset.Config.FourLaneDrums.ActivationNote = color.ToSystemColor())},
            };

            _allSettings[GameMode.FiveLaneDrums] = new Dictionary<string, ColorSetting>()
            {
                { nameof(ColorProfile.FiveLaneDrumsColors.KickFret       ), new ColorSetting(_preset.Config.FiveLaneDrums.KickFret.ToUnityColor(),        true, color => _preset.Config.FiveLaneDrums.KickFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.RedFret        ), new ColorSetting(_preset.Config.FiveLaneDrums.RedFret.ToUnityColor(),         true, color => _preset.Config.FiveLaneDrums.RedFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.YellowFret     ), new ColorSetting(_preset.Config.FiveLaneDrums.YellowFret.ToUnityColor(),      true, color => _preset.Config.FiveLaneDrums.YellowFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.BlueFret       ), new ColorSetting(_preset.Config.FiveLaneDrums.BlueFret.ToUnityColor(),        true, color => _preset.Config.FiveLaneDrums.BlueFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.OrangeFret     ), new ColorSetting(_preset.Config.FiveLaneDrums.OrangeFret.ToUnityColor(),      true, color => _preset.Config.FiveLaneDrums.OrangeFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.GreenFret      ), new ColorSetting(_preset.Config.FiveLaneDrums.GreenFret.ToUnityColor(),       true, color => _preset.Config.FiveLaneDrums.GreenFret = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.KickFretInner  ), new ColorSetting(_preset.Config.FiveLaneDrums.KickFretInner.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.KickFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.RedFretInner   ), new ColorSetting(_preset.Config.FiveLaneDrums.RedFretInner.ToUnityColor(),    true, color => _preset.Config.FiveLaneDrums.RedFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.YellowFretInner), new ColorSetting(_preset.Config.FiveLaneDrums.YellowFretInner.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.YellowFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.BlueFretInner  ), new ColorSetting(_preset.Config.FiveLaneDrums.BlueFretInner.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.BlueFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.OrangeFretInner), new ColorSetting(_preset.Config.FiveLaneDrums.OrangeFretInner.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.OrangeFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.GreenFretInner ), new ColorSetting(_preset.Config.FiveLaneDrums.GreenFretInner.ToUnityColor(),  true, color => _preset.Config.FiveLaneDrums.GreenFretInner = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.KickParticles  ), new ColorSetting(_preset.Config.FiveLaneDrums.KickParticles.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.KickParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.RedParticles   ), new ColorSetting(_preset.Config.FiveLaneDrums.RedParticles.ToUnityColor(),    true, color => _preset.Config.FiveLaneDrums.RedParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.YellowParticles), new ColorSetting(_preset.Config.FiveLaneDrums.YellowParticles.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.YellowParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.BlueParticles  ), new ColorSetting(_preset.Config.FiveLaneDrums.BlueParticles.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.BlueParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.OrangeParticles), new ColorSetting(_preset.Config.FiveLaneDrums.OrangeParticles.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.OrangeParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.GreenParticles ), new ColorSetting(_preset.Config.FiveLaneDrums.GreenParticles.ToUnityColor(),  true, color => _preset.Config.FiveLaneDrums.GreenParticles = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.KickNote       ), new ColorSetting(_preset.Config.FiveLaneDrums.KickNote.ToUnityColor(),        true, color => _preset.Config.FiveLaneDrums.KickNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.RedNote        ), new ColorSetting(_preset.Config.FiveLaneDrums.RedNote.ToUnityColor(),         true, color => _preset.Config.FiveLaneDrums.RedNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.YellowNote     ), new ColorSetting(_preset.Config.FiveLaneDrums.YellowNote.ToUnityColor(),      true, color => _preset.Config.FiveLaneDrums.YellowNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.BlueNote       ), new ColorSetting(_preset.Config.FiveLaneDrums.BlueNote.ToUnityColor(),        true, color => _preset.Config.FiveLaneDrums.BlueNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.OrangeNote     ), new ColorSetting(_preset.Config.FiveLaneDrums.OrangeNote.ToUnityColor(),      true, color => _preset.Config.FiveLaneDrums.OrangeNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.GreenNote      ), new ColorSetting(_preset.Config.FiveLaneDrums.GreenNote.ToUnityColor(),       true, color => _preset.Config.FiveLaneDrums.GreenNote = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.KickStarpower  ), new ColorSetting(_preset.Config.FiveLaneDrums.KickStarpower.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.KickStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.RedStarpower   ), new ColorSetting(_preset.Config.FiveLaneDrums.RedStarpower.ToUnityColor(),    true, color => _preset.Config.FiveLaneDrums.RedStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.YellowStarpower), new ColorSetting(_preset.Config.FiveLaneDrums.YellowStarpower.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.YellowStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.BlueStarpower  ), new ColorSetting(_preset.Config.FiveLaneDrums.BlueStarpower.ToUnityColor(),   true, color => _preset.Config.FiveLaneDrums.BlueStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.OrangeStarpower), new ColorSetting(_preset.Config.FiveLaneDrums.OrangeStarpower.ToUnityColor(), true, color => _preset.Config.FiveLaneDrums.OrangeStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.GreenStarpower ), new ColorSetting(_preset.Config.FiveLaneDrums.GreenStarpower.ToUnityColor(),  true, color => _preset.Config.FiveLaneDrums.GreenStarpower = color.ToSystemColor())},
                { nameof(ColorProfile.FiveLaneDrumsColors.ActivationNote ), new ColorSetting(_preset.Config.FiveLaneDrums.ActivationNote.ToUnityColor(),  true, color => _preset.Config.FiveLaneDrums.ActivationNote = color.ToSystemColor())},
            };
        }

        public override PresetContainer<ColorProfile> Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                var settings = _allSettings[_mode];
                switch (_mode)
                {
                    case GameMode.FiveFretGuitar:
                        ref readonly var fivefret = ref _preset.Config.FiveFretGuitar;
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OpenFret)].SetValueWithoutNotify(fivefret.OpenFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.GreenFret)].SetValueWithoutNotify(fivefret.GreenFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.RedFret)].SetValueWithoutNotify(fivefret.RedFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.YellowFret)].SetValueWithoutNotify(fivefret.YellowFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.BlueFret)].SetValueWithoutNotify(fivefret.BlueFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OrangeFret)].SetValueWithoutNotify(fivefret.OrangeFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OpenFretInner)].SetValueWithoutNotify(fivefret.OpenFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.GreenFretInner)].SetValueWithoutNotify(fivefret.GreenFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.RedFretInner)].SetValueWithoutNotify(fivefret.RedFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.YellowFretInner)].SetValueWithoutNotify(fivefret.YellowFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.BlueFretInner)].SetValueWithoutNotify(fivefret.BlueFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OrangeFretInner)].SetValueWithoutNotify(fivefret.OrangeFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OpenParticles)].SetValueWithoutNotify(fivefret.OpenParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.GreenParticles)].SetValueWithoutNotify(fivefret.GreenParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.RedParticles)].SetValueWithoutNotify(fivefret.RedParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.YellowParticles)].SetValueWithoutNotify(fivefret.YellowParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.BlueParticles)].SetValueWithoutNotify(fivefret.BlueParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OrangeParticles)].SetValueWithoutNotify(fivefret.OrangeParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OpenNote)].SetValueWithoutNotify(fivefret.OpenNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.GreenNote)].SetValueWithoutNotify(fivefret.GreenNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.RedNote)].SetValueWithoutNotify(fivefret.RedNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.YellowNote)].SetValueWithoutNotify(fivefret.YellowNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.BlueNote)].SetValueWithoutNotify(fivefret.BlueNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OrangeNote)].SetValueWithoutNotify(fivefret.OrangeNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OpenNoteStarPower)].SetValueWithoutNotify(fivefret.OpenNoteStarPower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.GreenNoteStarPower)].SetValueWithoutNotify(fivefret.GreenNoteStarPower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.RedNoteStarPower)].SetValueWithoutNotify(fivefret.RedNoteStarPower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.YellowNoteStarPower)].SetValueWithoutNotify(fivefret.YellowNoteStarPower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.BlueNoteStarPower)].SetValueWithoutNotify(fivefret.BlueNoteStarPower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveFretGuitarColors.OrangeNoteStarPower)].SetValueWithoutNotify(fivefret.OrangeNoteStarPower.ToUnityColor());
                        break;
                    case GameMode.FourLaneDrums:
                        ref readonly var fourlane = ref _preset.Config.FourLaneDrums;
                        settings[nameof(ColorProfile.FourLaneDrumsColors.KickFret)].SetValueWithoutNotify(fourlane.KickFret.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedFret)].SetValueWithoutNotify(fourlane.RedFret.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowFret)].SetValueWithoutNotify(fourlane.YellowFret.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueFret)].SetValueWithoutNotify(fourlane.BlueFret.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenFret)].SetValueWithoutNotify(fourlane.GreenFret.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.KickFretInner)].SetValueWithoutNotify(fourlane.KickFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedFretInner)].SetValueWithoutNotify(fourlane.RedFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowFretInner)].SetValueWithoutNotify(fourlane.YellowFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueFretInner)].SetValueWithoutNotify(fourlane.BlueFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenFretInner)].SetValueWithoutNotify(fourlane.GreenFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.KickParticles)].SetValueWithoutNotify(fourlane.KickParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedParticles)].SetValueWithoutNotify(fourlane.RedParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowParticles)].SetValueWithoutNotify(fourlane.YellowParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueParticles)].SetValueWithoutNotify(fourlane.BlueParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenParticles)].SetValueWithoutNotify(fourlane.GreenParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.KickNote)].SetValueWithoutNotify(fourlane.KickNote.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedDrum)].SetValueWithoutNotify(fourlane.RedDrum.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowDrum)].SetValueWithoutNotify(fourlane.YellowDrum.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueDrum)].SetValueWithoutNotify(fourlane.BlueDrum.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenDrum)].SetValueWithoutNotify(fourlane.GreenDrum.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedCymbal)].SetValueWithoutNotify(fourlane.RedCymbal.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowCymbal)].SetValueWithoutNotify(fourlane.YellowCymbal.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueCymbal)].SetValueWithoutNotify(fourlane.BlueCymbal.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenCymbal)].SetValueWithoutNotify(fourlane.GreenCymbal.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.KickStarpower)].SetValueWithoutNotify(fourlane.KickStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedDrumStarpower)].SetValueWithoutNotify(fourlane.RedDrumStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowDrumStarpower)].SetValueWithoutNotify(fourlane.YellowDrumStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueDrumStarpower)].SetValueWithoutNotify(fourlane.BlueDrumStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenDrumStarpower)].SetValueWithoutNotify(fourlane.GreenDrumStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.RedCymbalStarpower)].SetValueWithoutNotify(fourlane.RedCymbalStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.YellowCymbalStarpower)].SetValueWithoutNotify(fourlane.YellowCymbalStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.BlueCymbalStarpower)].SetValueWithoutNotify(fourlane.BlueCymbalStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.GreenCymbalStarpower)].SetValueWithoutNotify(fourlane.GreenCymbalStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FourLaneDrumsColors.ActivationNote)].SetValueWithoutNotify(fourlane.ActivationNote.ToUnityColor());
                        break;
                    case GameMode.FiveLaneDrums:
                        ref readonly var fivelane = ref _preset.Config.FiveLaneDrums;
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.KickFret)].SetValueWithoutNotify(fivelane.KickFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.RedFret)].SetValueWithoutNotify(fivelane.RedFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.YellowFret)].SetValueWithoutNotify(fivelane.YellowFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.BlueFret)].SetValueWithoutNotify(fivelane.BlueFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.OrangeFret)].SetValueWithoutNotify(fivelane.OrangeFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.GreenFret)].SetValueWithoutNotify(fivelane.GreenFret.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.KickFretInner)].SetValueWithoutNotify(fivelane.KickFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.RedFretInner)].SetValueWithoutNotify(fivelane.RedFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.YellowFretInner)].SetValueWithoutNotify(fivelane.YellowFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.BlueFretInner)].SetValueWithoutNotify(fivelane.BlueFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.OrangeFretInner)].SetValueWithoutNotify(fivelane.OrangeFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.GreenFretInner)].SetValueWithoutNotify(fivelane.GreenFretInner.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.KickParticles)].SetValueWithoutNotify(fivelane.KickParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.RedParticles)].SetValueWithoutNotify(fivelane.RedParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.YellowParticles)].SetValueWithoutNotify(fivelane.YellowParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.BlueParticles)].SetValueWithoutNotify(fivelane.BlueParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.OrangeParticles)].SetValueWithoutNotify(fivelane.OrangeParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.GreenParticles)].SetValueWithoutNotify(fivelane.GreenParticles.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.KickNote)].SetValueWithoutNotify(fivelane.KickNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.RedNote)].SetValueWithoutNotify(fivelane.RedNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.YellowNote)].SetValueWithoutNotify(fivelane.YellowNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.BlueNote)].SetValueWithoutNotify(fivelane.BlueNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.OrangeNote)].SetValueWithoutNotify(fivelane.OrangeNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.GreenNote)].SetValueWithoutNotify(fivelane.GreenNote.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.KickStarpower)].SetValueWithoutNotify(fivelane.KickStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.RedStarpower)].SetValueWithoutNotify(fivelane.RedStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.YellowStarpower)].SetValueWithoutNotify(fivelane.YellowStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.BlueStarpower)].SetValueWithoutNotify(fivelane.BlueStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.OrangeStarpower)].SetValueWithoutNotify(fivelane.OrangeStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.GreenStarpower)].SetValueWithoutNotify(fivelane.GreenStarpower.ToUnityColor());
                        settings[nameof(ColorProfile.FiveLaneDrumsColors.ActivationNote)].SetValueWithoutNotify(fivelane.ActivationNote.ToUnityColor());
                        break;
                }
                SettingsMenu.Instance.OnSettingChanged();
            }
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            _settingFields.Clear();
            // Create instrument dropdown
            CreateField(settingContainer, navGroup, nameof(ColorProfile), "Instrument",
                new DropdownSetting<GameMode>(_mode, RefreshForSubSection)
                {
                    GameMode.FiveFretGuitar,
                    GameMode.FourLaneDrums,
                    GameMode.FiveLaneDrums,
                }
            );

            // Header
            SpawnHeader(settingContainer, "PresetSettings");
            CreateFields(settingContainer, navGroup, nameof(ColorProfile), _allSettings[_mode]);
        }

        private void RefreshForSubSection(GameMode mode)
        {
            _mode = mode;
            SettingsMenu.Instance.Refresh();
        }
    }

    public sealed class EnginePresetSubTab : PresetSubTab<EnginePreset>
    {
        private readonly struct GuitarSettings
        {
            public readonly ToggleSetting AntiGhosting;
            public readonly ToggleSetting InfiniteFrontEnd;
            public readonly DurationSetting HopoLeniency;
            public readonly DurationSetting StrumLeniency;
            public readonly DurationSetting StrumLeniencySmall;
            public readonly ToggleSetting DynamicHitWindow;
            public readonly HitWindowSetting HitWindow;
            public readonly SliderSetting WindowFrontToBack;

            public GuitarSettings(ToggleSetting antiGhosting, ToggleSetting infiniteFrontEnd, DurationSetting hopoLeniency, DurationSetting strumLeniency, DurationSetting strumLeniencySmall, ToggleSetting dynamicHitWindow, HitWindowSetting hitWindow, SliderSetting windowFrontToBack)
            {
                AntiGhosting = antiGhosting;
                InfiniteFrontEnd = infiniteFrontEnd;
                HopoLeniency = hopoLeniency;
                StrumLeniency = strumLeniency;
                StrumLeniencySmall = strumLeniencySmall;
                DynamicHitWindow = dynamicHitWindow;
                HitWindow = hitWindow;
                WindowFrontToBack = windowFrontToBack;
            }
        }

        private readonly struct DrumSettings
        {
            public readonly ToggleSetting DynamicHitWindow;
            public readonly HitWindowSetting HitWindow;
            public readonly SliderSetting WindowFrontToBack;

            public DrumSettings(ToggleSetting dynamicHitWindow, HitWindowSetting hitWindow, SliderSetting windowFrontToBack)
            {
                DynamicHitWindow = dynamicHitWindow;
                HitWindow = hitWindow;
                WindowFrontToBack = windowFrontToBack;
            }
        }

        private readonly struct VocalSettings
        {
            public readonly SliderSetting WindowSizeE;
            public readonly SliderSetting WindowSizeM;
            public readonly SliderSetting WindowSizeH;
            public readonly SliderSetting WindowSizeX;
            public readonly SliderSetting HitPercentE;
            public readonly SliderSetting HitPercentM;
            public readonly SliderSetting HitPercentH;
            public readonly SliderSetting HitPercentX;

            public VocalSettings(SliderSetting windowSizeE, SliderSetting windowSizeM, SliderSetting windowSizeH, SliderSetting windowSizeX, SliderSetting hitPercentE, SliderSetting hitPercentM, SliderSetting hitPercentH, SliderSetting hitPercentX)
            {
                WindowSizeE = windowSizeE;
                WindowSizeM = windowSizeM;
                WindowSizeH = windowSizeH;
                WindowSizeX = windowSizeX;
                HitPercentE = hitPercentE;
                HitPercentM = hitPercentM;
                HitPercentH = hitPercentH;
                HitPercentX = hitPercentX;
            }
        }
        private const DurationInputField.Unit ENGINE_UNIT = DurationInputField.Unit.Milliseconds;

        private EnginePreset.Type _type = EnginePreset.Type.FiveFretGuitarPreset;
        private readonly GuitarSettings _guitar;
        private readonly DrumSettings _drum;
        private readonly VocalSettings _vocal;

        public EnginePresetSubTab(CustomContentContainer<EnginePreset> customContent, IPreviewBuilder previewBuilder = null)
            : base(customContent, EnginePreset.Default, previewBuilder)
        {
            _guitar = new GuitarSettings
            (
                new ToggleSetting(_preset.Config.FiveFretGuitar.AntiGhosting, val => _preset.Config.FiveFretGuitar.AntiGhosting = val),
                new ToggleSetting(_preset.Config.FiveFretGuitar.InfiniteFrontEnd, val => _preset.Config.FiveFretGuitar.InfiniteFrontEnd = val),
                new DurationSetting(_preset.Config.FiveFretGuitar.HopoLeniency, ENGINE_UNIT, double.PositiveInfinity, val => _preset.Config.FiveFretGuitar.HopoLeniency = val),
                new DurationSetting(_preset.Config.FiveFretGuitar.StrumLeniency, ENGINE_UNIT, double.PositiveInfinity, val => _preset.Config.FiveFretGuitar.StrumLeniency = val),
                new DurationSetting(_preset.Config.FiveFretGuitar.StrumLeniencySmall, ENGINE_UNIT, double.PositiveInfinity, val => _preset.Config.FiveFretGuitar.StrumLeniencySmall = val),
                new ToggleSetting(_preset.Config.FiveFretGuitar.HitWindow.IsDynamic, val =>
                {
                    // If this gets called, it refreshes before it can update.
                    // We must update the dynamic hit window bool here.
                    _preset.Config.FiveFretGuitar.HitWindow.IsDynamic = val;

                    SettingsMenu.Instance.RefreshAndKeepPosition();
                }),
                new HitWindowSetting(_preset.Config.FiveFretGuitar.HitWindow, val => _preset.Config.FiveFretGuitar.HitWindow = val),
                new SliderSetting((float) _preset.Config.FiveFretGuitar.HitWindow.FrontToBackRatio, 0f, 2f, val => _preset.Config.FiveFretGuitar.HitWindow.FrontToBackRatio = val)
            );

            _drum = new DrumSettings
            (
                new ToggleSetting(_preset.Config.Drums.HitWindow.IsDynamic, (value) =>
                {
                    // If this gets called, it refreshes before it can update.
                    // We must update the dynamic hit window bool here.
                    _preset.Config.Drums.HitWindow.IsDynamic = value;

                    SettingsMenu.Instance.RefreshAndKeepPosition();
                }),
                new HitWindowSetting(_preset.Config.Drums.HitWindow, val => _preset.Config.Drums.HitWindow = val),
                new SliderSetting((float)_preset.Config.Drums.HitWindow.FrontToBackRatio, 0f, 2f, val => _preset.Config.Drums.HitWindow.FrontToBackRatio = val)
            );
            
            _vocal = new VocalSettings
            (
                new SliderSetting((float)_preset.Config.Vocals.WindowSizeE, 0f, 3f, val => _preset.Config.Vocals.WindowSizeE = val),
                new SliderSetting((float)_preset.Config.Vocals.WindowSizeM, 0f, 3f, val => _preset.Config.Vocals.WindowSizeM = val),
                new SliderSetting((float)_preset.Config.Vocals.WindowSizeH, 0f, 3f, val => _preset.Config.Vocals.WindowSizeH = val),
                new SliderSetting((float)_preset.Config.Vocals.WindowSizeX, 0f, 3f, val => _preset.Config.Vocals.WindowSizeX = val),
                new SliderSetting((float)_preset.Config.Vocals.HitPercentE, 0f, 1f, val => _preset.Config.Vocals.HitPercentE = val),
                new SliderSetting((float)_preset.Config.Vocals.HitPercentM, 0f, 1f, val => _preset.Config.Vocals.HitPercentM = val),
                new SliderSetting((float)_preset.Config.Vocals.HitPercentH, 0f, 1f, val => _preset.Config.Vocals.HitPercentH = val),
                new SliderSetting((float)_preset.Config.Vocals.HitPercentX, 0f, 1f, val => _preset.Config.Vocals.HitPercentX = val)
            );
        }

        public override PresetContainer<EnginePreset> Preset
        {
            get => _preset;
            set
            {
                _preset = value;
                switch (_type)
                {
                    case EnginePreset.Type.FiveFretGuitarPreset:
                        ref readonly var fivefret = ref _preset.Config.FiveFretGuitar;
                        _guitar.AntiGhosting.SetValueWithoutNotify(fivefret.AntiGhosting);
                        _guitar.InfiniteFrontEnd.SetValueWithoutNotify(fivefret.InfiniteFrontEnd);
                        _guitar.HopoLeniency.SetValueWithoutNotify(fivefret.HopoLeniency);
                        _guitar.StrumLeniency.SetValueWithoutNotify(fivefret.StrumLeniency);
                        _guitar.StrumLeniencySmall.SetValueWithoutNotify(fivefret.StrumLeniencySmall);
                        _guitar.DynamicHitWindow.SetValueWithoutNotify(fivefret.HitWindow.IsDynamic);
                        _guitar.HitWindow.SetValueWithoutNotify(fivefret.HitWindow);
                        _guitar.WindowFrontToBack.SetValueWithoutNotify((float) fivefret.HitWindow.FrontToBackRatio);
                        break;
                    case EnginePreset.Type.DrumsPreset:
                        ref readonly var drums = ref _preset.Config.Drums;
                        _drum.DynamicHitWindow.SetValueWithoutNotify(drums.HitWindow.IsDynamic);
                        _drum.HitWindow.SetValueWithoutNotify(drums.HitWindow);
                        _drum.WindowFrontToBack.SetValueWithoutNotify((float) drums.HitWindow.FrontToBackRatio);
                        break;
                    case EnginePreset.Type.VocalsPreset:
                        ref readonly var vocals = ref _preset.Config.Vocals;
                        _vocal.WindowSizeE.SetValueWithoutNotify((float) vocals.WindowSizeE);
                        _vocal.WindowSizeM.SetValueWithoutNotify((float) vocals.WindowSizeM);
                        _vocal.WindowSizeH.SetValueWithoutNotify((float) vocals.WindowSizeH);
                        _vocal.WindowSizeX.SetValueWithoutNotify((float) vocals.WindowSizeX);
                        _vocal.HitPercentE.SetValueWithoutNotify((float) vocals.HitPercentE);
                        _vocal.HitPercentM.SetValueWithoutNotify((float) vocals.HitPercentM);
                        _vocal.HitPercentH.SetValueWithoutNotify((float) vocals.HitPercentH);
                        _vocal.HitPercentX.SetValueWithoutNotify((float) vocals.HitPercentX);
                        break;
                }
                SettingsMenu.Instance.OnSettingChanged();
            }
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            _settingFields.Clear();
            // Create game mode dropdown
            CreateField(settingContainer, navGroup, nameof(EnginePreset), "GameMode",
                new DropdownSetting<EnginePreset.Type>(_type, RefreshForSubSection)
                {
                    EnginePreset.Type.FiveFretGuitarPreset,
                    EnginePreset.Type.DrumsPreset,
                    EnginePreset.Type.VocalsPreset
                }
            );

            // Set the preview type
            if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
            {
                trackPreviewBuilder.StartingGameMode = _type switch
                {
                    EnginePreset.Type.FiveFretGuitarPreset => GameMode.FiveFretGuitar,
                    EnginePreset.Type.DrumsPreset          => GameMode.FourLaneDrums,
                    // EnginePreset.Type.VocalsPreset      => GameMode.Vocals, // Uncomment once we have vocals visual preview
                    EnginePreset.Type.VocalsPreset         => trackPreviewBuilder.StartingGameMode, // Do not change
                    _ => throw new System.Exception("Unreachable.")
                };
            }
            else
            {
                YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
            }

            // Header
            SpawnHeader(settingContainer, "PresetSettings");

            // Spawn in the correct settings
            switch (_type)
            {
                case EnginePreset.Type.FiveFretGuitarPreset:
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.AntiGhosting),               _guitar.AntiGhosting);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.InfiniteFrontEnd),           _guitar.InfiniteFrontEnd);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.HopoLeniency),               _guitar.HopoLeniency);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.StrumLeniency),              _guitar.StrumLeniency);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.StrumLeniencySmall),         _guitar.StrumLeniencySmall);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.HitWindow.IsDynamic),        _guitar.DynamicHitWindow);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.HitWindow),                  _guitar.HitWindow);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.FiveFretGuitarPreset.HitWindow.FrontToBackRatio), _guitar.WindowFrontToBack);
                    break;
                case EnginePreset.Type.DrumsPreset:
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.DrumsPreset.HitWindow.IsDynamic),        _drum.DynamicHitWindow);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.DrumsPreset.HitWindow),                  _drum.HitWindow);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.DrumsPreset.HitWindow.FrontToBackRatio), _drum.WindowFrontToBack);
                    break;
                case EnginePreset.Type.VocalsPreset:
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.WindowSizeE), _vocal.WindowSizeE);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.WindowSizeM), _vocal.WindowSizeM);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.WindowSizeH), _vocal.WindowSizeH);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.WindowSizeX), _vocal.WindowSizeX);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.HitPercentE), _vocal.HitPercentE);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.HitPercentM), _vocal.HitPercentM);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.HitPercentH), _vocal.HitPercentH);
                    CreateField(settingContainer, navGroup, nameof(EnginePreset), nameof(EnginePreset.VocalsPreset.HitPercentX), _vocal.HitPercentX);
                    break;
                default:
                    throw new System.Exception("Unreachable");
            }
        }

        private void RefreshForSubSection(EnginePreset.Type type)
        {
            _type = type;
            SettingsMenu.Instance.Refresh();
        }
    }
}