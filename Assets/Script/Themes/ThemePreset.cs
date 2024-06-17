using Newtonsoft.Json;
using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Themes
{
    public partial struct ThemePreset
    {
        public string AssetBundleThemePath;
        public Guid PreferredColorProfile;
        public Guid PreferredCameraPreset;

        public readonly GameMode[] SupportedGameModes;

        public ThemePreset(params GameMode[] supportedGameModes)
        {
            AssetBundleThemePath = string.Empty;
            PreferredColorProfile = Guid.Empty;
            PreferredCameraPreset = Guid.Empty;
            SupportedGameModes = supportedGameModes;
        }

        public readonly ThemeContainer CreateThemeContainer()
        {
            var themePrefab = Addressables
                    .LoadAssetAsync<GameObject>(AssetBundleThemePath)
                    .WaitForCompletion();

            return new ThemeContainer(themePrefab, true);
        }
    }
}