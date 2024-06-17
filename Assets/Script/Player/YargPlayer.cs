using System;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Input;
using YARG.Settings.Customization;
using YARG.Themes;

namespace YARG.Player
{
    public class YargPlayer : IDisposable
    {
        private YargProfile _profile;
        private bool _inputsEnabled;
        private ProfileBindings _bindings;
        private PresetContainer<EnginePreset> _enginePreset;
        private PresetContainer<ThemePreset > _themePreset;
        private PresetContainer<ColorProfile> _colorProfile;
        private PresetContainer<CameraPreset> _cameraPreset;

        public event MenuInputEvent MenuInput;

        /// <summary>
        /// Whether or not the player is sitting out. This is not needed in <see cref="Profile"/> as
        /// players that are sitting out are not included in replays.
        /// </summary>
        public bool SittingOut;

        public YargProfile Profile => _profile;
        public bool InputsEnabled => _inputsEnabled;
        public ProfileBindings Bindings => _bindings;
        public ref readonly PresetContainer<EnginePreset> EnginePreset => ref _enginePreset;
        public ref readonly PresetContainer<ThemePreset > ThemePreset => ref _themePreset;
        public ref readonly PresetContainer<ColorProfile> ColorProfile => ref _colorProfile;
        public ref readonly PresetContainer<CameraPreset> CameraPreset => ref _cameraPreset;

        /// <summary>
        /// Overrides the engine parameters in the gameplay player.
        /// This is only used when loading replays.
        /// </summary>
        public BaseEngineParameters EngineParameterOverride { get; set; }

        public YargPlayer(YargProfile profile, ProfileBindings bindings, bool resolveDevices)
        {
            SwapToProfile(profile, bindings, resolveDevices);
            SetPresetsFromProfile();
        }

        public void SwapToProfile(YargProfile profile, ProfileBindings bindings, bool resolveDevices)
        {
            // Force-disable inputs
            bool enabled = _inputsEnabled;
            DisableInputs();

            // Swap to the new profile
            Bindings?.Dispose();
            _profile = profile;
            _bindings = bindings;

            // Resolve bindings
            if (resolveDevices)
            {
                _bindings?.ResolveDevices();
            }

            // Re-enable inputs
            if (enabled)
            {
                EnableInputs();
            }
        }

        public void SetPresetsFromProfile()
        {
            if (!CustomContentManager.EnginePresets.TryGetPreset(Profile.EnginePreset, out _enginePreset))
            {
                _enginePreset = Core.Game.EnginePreset.Default;
            }

            if (!CustomContentManager.ThemePresets.TryGetPreset(Profile.ThemePreset, out _themePreset))
            {
                _themePreset = Themes.ThemePreset.Default;
            }

            if (!CustomContentManager.ColorProfiles.TryGetPreset(Profile.ColorProfile, out _colorProfile))
            {
                _colorProfile = Core.Game.ColorProfile.Default;
            }

            if (!CustomContentManager.CameraSettings.TryGetPreset(Profile.CameraPreset, out _cameraPreset))
            {
                _cameraPreset = Core.Game.CameraPreset.Default;
            }
        }

        public void SetPresetsFromReplay(ReplayPresetContainer presetContainer)
        {
            if (presetContainer.TryGetColorProfile(Profile.ColorProfile, out var profile))
            {
                _colorProfile = profile;
            }

            if (presetContainer.TryGetCameraPreset(Profile.CameraPreset, out var camera))
            {
                _cameraPreset = camera;
            }
        }

        public void EnableInputs()
        {
            if (_inputsEnabled || Bindings == null)
                return;

            Bindings.EnableInputs();
            Bindings.MenuInputProcessed += OnMenuInput;
            InputManager.RegisterPlayer(this);

            _inputsEnabled = true;
        }

        public void DisableInputs()
        {
            if (!_inputsEnabled || Bindings == null)
                return;

            Bindings.DisableInputs();
            Bindings.MenuInputProcessed -= OnMenuInput;
            InputManager.UnregisterPlayer(this);

            _inputsEnabled = false;
        }

        private void OnMenuInput(ref GameInput input)
        {
            MenuInput?.Invoke(this, ref input);
        }

        public void Dispose()
        {
            DisableInputs();
            Bindings?.Dispose();
        }
    }
}