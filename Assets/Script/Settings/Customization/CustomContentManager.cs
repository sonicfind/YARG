using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Utility;
using YARG.Helpers;
using YARG.Themes;

namespace YARG.Settings.Customization
{
    public static class CustomContentManager
    {
        private const string CUSTOM_DIRECTORY = "custom";
        private const string COLOR_DIRECTORY = "colors";
        private const string CAMERA_DIRECTORY = "cameras";
        private const string THEME_DIRECTORY = "themes";
        private const string ENGINE_DIRECTORY = "enginePresets";

        public const string COLORS_FILE = "colors.json";
        public const string CAMERAS_FILE = "cameras.json";
        public const string THEMES_FILE = "themes.json";
        public const string ENGINES_FILE = "engines.json";

        public static readonly string CustomizationDirectory = Path.Combine(PathHelper.PersistentDataPath, CUSTOM_DIRECTORY);

        private static CustomContentContainer<ColorProfile> _colorProfiles  = new(ColorProfile.Defaults, COLOR_DIRECTORY, COLORS_FILE);
        private static CustomContentContainer<CameraPreset> _cameraSettings = new(CameraPreset.Defaults, CAMERA_DIRECTORY, CAMERAS_FILE);
        private static CustomContentContainer<ThemePreset>  _themePresets   = new(ThemePreset.Defaults, THEME_DIRECTORY, THEMES_FILE);
        private static CustomContentContainer<EnginePreset> _enginePresets  = new(EnginePreset.Defaults, ENGINE_DIRECTORY, ENGINES_FILE);

        public static CustomContentContainer<ColorProfile> ColorProfiles  => _colorProfiles;
        public static CustomContentContainer<CameraPreset> CameraSettings => _cameraSettings;
        public static CustomContentContainer<ThemePreset>  ThemePresets   => _themePresets;
        public static CustomContentContainer<EnginePreset> EnginePresets  => _enginePresets;

        static CustomContentManager()
        {
            _colorProfiles.LoadPresetsFromFile();
            _cameraSettings.LoadPresetsFromFile();
            _themePresets.LoadPresetsFromFile();
            _enginePresets.LoadPresetsFromFile();
        }

        public static void SaveAll()
        {
            _colorProfiles.SavePresetsToFile();
            _cameraSettings.SavePresetsToFile();
            _themePresets.SavePresetsToFile();
            _enginePresets.SavePresetsToFile();
        }
    }
}