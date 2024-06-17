using System.Collections.Generic;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Themes
{
    public partial struct ThemePreset
    {
        public static PresetContainer<ThemePreset> Default = new("Rectangular",
            new ThemePreset(GameMode.FiveFretGuitar, GameMode.SixFretGuitar,
                            GameMode.FourLaneDrums,  GameMode.FiveLaneDrums,
                            GameMode.ProKeys)
        {
            AssetBundleThemePath = "Themes/Rectangular",
            PreferredColorProfile = ColorProfile.Default.Id,
            PreferredCameraPreset = CameraPreset.Default.Id
        });

        public static readonly PresetContainer<ThemePreset>[] Defaults =
        {
            Default,
            new("Circular (Beta)", new ThemePreset(GameMode.FiveFretGuitar)
            {
                AssetBundleThemePath = "Themes/Circular",
                PreferredColorProfile = ColorProfile.CircularDefault.Id,
                PreferredCameraPreset = CameraPreset.CircularDefault.Id,
            }),
            new("YARG on Fire", new ThemePreset(GameMode.FiveFretGuitar,
                                                GameMode.FourLaneDrums,
                                                GameMode.FiveLaneDrums)
            {
                AssetBundleThemePath = "Themes/AprilFools",
                PreferredColorProfile = ColorProfile.AprilFoolsDefault.Id,
                PreferredCameraPreset = CameraPreset.CircularDefault.Id,
            })
        };

        public static bool IsDefault(in PresetContainer<ThemePreset> theme)
        {
            foreach (var def in Defaults)
            {
                if (def.Id == theme.Id)
                {
                    return true;
                }
            }
            return false;
        }
    }
}