using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DG.Tweening;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using TrombLoader.Helpers;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Chart;
using YARG.Data;
using YARG.Input;
using YARG.Serialization;
using YARG.Serialization.Parser;
using YARG.Settings;
using YARG.Song;
using YARG.Song.Chart;
using YARG.Song.Chart.Notes;
using YARG.Song.Entries;
using YARG.UI;
using YARG.Venue;

namespace YARG.PlayMode
{
    public class AudioTrackNode
    {
        public readonly Instrument baseInstrument;
        public readonly Instrument realInstrument;
        public readonly SongStem[] stems;
        public AudioTrackNode(Instrument baseInstrument, Instrument realInstrument, SongStem[] stems)
        {
            this.baseInstrument = baseInstrument;
            this.realInstrument = realInstrument;
            this.stems = stems;
        }
    }

    public class Play : MonoBehaviour
    {
        public static Play Instance { get; private set; }

        public static float speed = 1f;

        public const float SONG_START_OFFSET = -2f;
        public const float SONG_END_DELAY = 2f;

        public delegate void BeatAction();

        public static event BeatAction BeatEvent;

        public delegate void SongStateChangeAction(SongEntry songInfo);

        public static event SongStateChangeAction OnSongStart;
        public static event SongStateChangeAction OnSongEnd;

        public delegate void PauseStateChangeAction(bool pause);

        public static event PauseStateChangeAction OnPauseToggle;

        public bool SongStarted { get; private set; }

        [field: SerializeField]
        public Camera DefaultCamera { get; private set; }

        private OccurrenceList<Instrument> audioLowering = new();
        private List<Instrument> audioReverb = new();
        private Dictionary<SongStem, (float percent, bool enabled)> audioPitchBend = new(
            AudioHelpers.PitchBendAllowedStems.Select((stem) => new KeyValuePair<SongStem, (float, bool)>(stem, (0f, false)))
        );

        private bool audioRunning;
        private float realSongTime;
        public float SongTime => realSongTime - PlayerManager.AudioCalibration * speed - Song.Delay;

        private float audioLength;
        public float SongLength { get; private set; }

        public YargChart chart;
        public YARGSong chartNew;

        [Space]
        [SerializeField]
        private GameObject playResultScreen;

        [SerializeField]
        private RawImage playCover;

        [SerializeField]
        private GameObject scoreDisplay;

        private int beatIndex = 0;

        // tempo (updated throughout play)
        public float CurrentBeatsPerSecond { get; private set; } = 0f;
        public float CurrentTempo => CurrentBeatsPerSecond * 60; // BPM

        private List<AbstractTrack> _tracks;

        public bool endReached { get; private set; } = false;

        private bool _paused = false;

        public bool Paused
        {
            get => _paused;
            set
            {
                // disable pausing once we reach end of song
                if (endReached) return;

                _paused = value;

                GameUI.Instance.pauseMenu.SetActive(value);

                if (value)
                {
                    Time.timeScale = 0f;

                    GameManager.AudioManager.Pause();

                    if (GameUI.Instance.videoPlayer.enabled)
                    {
                        GameUI.Instance.videoPlayer.Pause();
                    }
                }
                else
                {
                    Time.timeScale = 1f;

                    GameManager.AudioManager.Play();

                    if (GameUI.Instance.videoPlayer.enabled)
                    {
                        GameUI.Instance.videoPlayer.Play();
                    }
                }

                OnPauseToggle?.Invoke(_paused);
            }
        }

        public SongEntry Song => GameManager.Instance.SelectedSong;

        private bool playingVocals = false;

        private readonly AudioTrackNode[] AudioArrays =
        {
            new(Instrument.GUITAR, Instrument.REAL_GUITAR, new[] { SongStem.Guitar }),
            new(Instrument.RHYTHM, Instrument.INVALID, new[] { SongStem.Rhythm }),
            new(Instrument.BASS,   Instrument.REAL_BASS, new[] { SongStem.Bass }),
            new(Instrument.KEYS,   Instrument.REAL_KEYS, new[] { SongStem.Keys }),
            new(Instrument.DRUMS,  Instrument.REAL_DRUMS, new[] { SongStem.Drums, SongStem.Drums1, SongStem.Drums2, SongStem.Drums3, SongStem.Drums4 }),
        };

        private readonly AudioTrackNode[] AudioArrays_Rhythmless =
        {
            new(Instrument.GUITAR, Instrument.REAL_GUITAR, new[] { SongStem.Guitar}),
            new(Instrument.BASS,  Instrument.REAL_BASS, new[] { SongStem.Bass, SongStem.Rhythm }),
            new(Instrument.KEYS,  Instrument.REAL_KEYS, new[] { SongStem.Keys }),
            new(Instrument.DRUMS, Instrument.REAL_DRUMS, new[] { SongStem.Drums, SongStem.Drums1, SongStem.Drums2, SongStem.Drums3, SongStem.Drums4 }),
        };

        private AudioTrackNode[] currAudioArrays = Array.Empty<AudioTrackNode>();

        private void Awake()
        {
            Instance = this;

            ScoreKeeper.Reset();
            StarScoreKeeper.Reset();

            // Force the music player to disable, and hide the help bar
            // This is mostly for "Test Play" mode
            Navigator.Instance.ForceHideMusicPlayer();
            Navigator.Instance.PopAllSchemes();

            // Song
            StartSong();
        }

        private void StartSong()
        {
            GameUI.Instance.SetLoadingText("Loading audio...");
            Song.LoadAudio(GameManager.AudioManager, speed);

            bool isYargSong = Song.Source.SortStr == "yarg";
            GameManager.AudioManager.Options.UseMinimumStemVolume = isYargSong;

            // Get song length
            audioLength = GameManager.AudioManager.AudioLengthF;
            SongLength = audioLength;

            GameUI.Instance.SetLoadingText("Loading chart...");

            LoadChart();

            if (chartNew.LastNoteTick <= chartNew.EndTick && chartNew.EndTick < SongLength)
                SongLength = chartNew.EndTick;

            // Finally, append some additional time so the song doesn't just end immediately
            SongLength += SONG_END_DELAY * speed;

            GameUI.Instance.SetLoadingText("Spawning tracks...");

            // Spawn tracks
            _tracks = new List<AbstractTrack>();
            currAudioArrays = AudioArrays_Rhythmless;
            int trackIndex = 0;
            foreach (var player in PlayerManager.players)
            {
                if (player.chosenInstrument == Instrument.INVALID)
                {
                    // Skip players that are sitting out
                    continue;
                }

                // Temporary, will make a better system later
                if (player.chosenInstrument == Instrument.RHYTHM)
                {
                    currAudioArrays = AudioArrays;
                }

                // Temporary, same here
                if (player.chosenInstrument is Instrument.VOCALS or Instrument.HARMONY)
                {
                    playingVocals = true;
                }

                string trackPath = player.inputStrategy.GetTrackPath();

                if (trackPath == null)
                {
                    continue;
                }

                var prefab = Addressables.LoadAssetAsync<GameObject>(trackPath).WaitForCompletion();
                var track = Instantiate(prefab, new Vector3(trackIndex * 25f, 100f, 0f), prefab.transform.rotation);
                _tracks.Add(track.GetComponent<AbstractTrack>());
                _tracks[trackIndex].player = player;

                trackIndex++;
            }

            // Load background (venue, video, image, etc.)
            LoadBackground();

            SongStarted = true;

            // Hide loading screen
            GameUI.Instance.loadingContainer.SetActive(false);

            realSongTime = SONG_START_OFFSET * speed;
            StartCoroutine(StartAudio());

            OnSongStart?.Invoke(Song);
        }

        private void LoadBackground()
        {
            var typePathPair = VenueLoader.GetVenuePath(Song);
            if (typePathPair == null)
            {
                return;
            }

            var type = typePathPair.Value.Type;
            var path = typePathPair.Value.Path;

            switch (type)
            {
                case VenueType.Yarground:
                    var bundle = AssetBundle.LoadFromFile(path);

                    // KEEP THIS PATH LOWERCASE
                    // Breaks things for other platforms, because Unity
                    var bg = bundle.LoadAsset<GameObject>(BundleBackgroundManager.BACKGROUND_PREFAB_PATH
                        .ToLowerInvariant());

                    // Fix for non-Windows machines
                    // Probably there's a better way to do this.
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX || UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                    Renderer[] renderers = bg.GetComponentsInChildren<Renderer>();

                    foreach (Renderer renderer in renderers) {
                        Material[] materials = renderer.sharedMaterials;

                        for (int i = 0; i < materials.Length; i++) {
                            Material material = materials[i];
                            material.shader = Shader.Find(material.shader.name);
                        }
                    }
#endif

                    var bgInstance = Instantiate(bg);

                    bgInstance.GetComponent<BundleBackgroundManager>().Bundle = bundle;
                    break;
                case VenueType.Video:
                    GameUI.Instance.videoPlayer.url = path;
                    GameUI.Instance.videoPlayer.enabled = true;
                    GameUI.Instance.videoPlayer.Prepare();
                    break;
                case VenueType.Image:
                    var png = ImageHelper.LoadTextureFromFile(path);
                    GameUI.Instance.background.texture = png;
                    break;
            }
        }

        private void LoadChart()
        {
            chart = SongContainer.Songs[1].LoadChart_Original();
            chartNew = Song.LoadChart();

            InputHandler handler = new(5);
            var players = chartNew.m_tracks.lead_5.SetupPlayers(new() { { 3, new[] { handler } } });

            // initialize current tempo
            if (chart.beats.Count > 2)
            {
                CurrentBeatsPerSecond = chart.beats[1].Time - chart.beats[0].Time;
            }
        }

        private IEnumerator StartAudio()
        {
            while (realSongTime < 0f)
            {
                // Wait until the song time is 0
                yield return null;
            }

            float? startVideoIn = null;
            if (GameUI.Instance.videoPlayer.enabled)
            {
                // Set the chart start offset here (if ini)
                if (Song is IniSongEntry ini)
                {
                    if (ini.VideoStartOffset < 0)
                    {
                        startVideoIn = Mathf.Abs(ini.VideoStartOffset / 1000f);
                    }
                    else
                    {
                        GameUI.Instance.videoPlayer.time = ini.VideoStartOffset / 1000.0;
                    }
                }

                // Play the video if a start time wasn't defined
                if (startVideoIn == null)
                {
                    GameUI.Instance.videoPlayer.Play();
                }
            }

            GameManager.AudioManager.Play();

            GameManager.AudioManager.SongEnd += OnEndReached;
            audioRunning = true;

            if (startVideoIn != null)
            {
                // Wait, then start on time
                yield return new WaitForSeconds(startVideoIn.Value);
                GameUI.Instance.videoPlayer.Play();
            }
        }

        private void Update()
        {
            if (!SongStarted)
            {
                return;
            }

            // Pausing
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Paused = !Paused;
            }

            if (Paused)
            {
                return;
            }

            // Update this every frame to make sure all notes are spawned at the same time.
            float audioTime = GameManager.AudioManager.CurrentPositionF;
            if (audioRunning && audioTime < audioLength)
            {
                realSongTime = audioTime;
            }
            else
            {
                // We need to update the song time ourselves if the audio finishes before the song actually ends
                realSongTime += Time.deltaTime * speed;
            }

            UpdateAudio();

            // Update whammy pitch state
            UpdateWhammyPitch();

            // Update beats
            while (chart.beats.Count > beatIndex && chart.beats[beatIndex].Time <= SongTime)
            {
                foreach (var track in _tracks)
                {
                    if (!track.IsStarPowerActive || !GameManager.AudioManager.Options.UseStarpowerFx)
                        continue;

                    GameManager.AudioManager.PlaySoundEffect(SfxSample.Clap);
                    break;
                }

                BeatEvent?.Invoke();
                beatIndex++;

                if (beatIndex < chart.beats.Count)
                {
                    CurrentBeatsPerSecond = 1 / (chart.beats[beatIndex].Time - chart.beats[beatIndex - 1].Time);
                }
            }

            // End song
            if (!endReached && realSongTime >= SongLength)
            {
                endReached = true;
                StartCoroutine(EndSong(true));
            }
        }

        private void OnEndReached()
        {
            audioLength = GameManager.AudioManager.CurrentPositionF;
            audioRunning = false;
        }

        private void UpdateAudio()
        {
            if (SettingsManager.Settings.MuteOnMiss.Data)
            {
                foreach (var node in currAudioArrays)
                    UpdateMuteOnMiss(node);
            }

            // Reverb audio with starpower

            if (GameManager.AudioManager.Options.UseStarpowerFx)
            {
                GameManager.AudioManager.ApplyReverb(SongStem.Song, audioReverb.Count > 0);

                foreach (var node in currAudioArrays)
                {
                    bool applyReverb = audioReverb.Contains(node.baseInstrument) || audioReverb.Contains(node.realInstrument);
                    foreach(var stem in node.stems)
                        GameManager.AudioManager.ApplyReverb(stem, applyReverb);
                }
            }
        }

        private void UpdateMuteOnMiss(AudioTrackNode audioNode)
        {
            // Get total amount of players with the instrument (and the amount lowered)
            int amountWithInstrument = PlayerManager.PlayersWithInstrument(audioNode.baseInstrument);
            int amountLowered = audioLowering.GetCount(audioNode.baseInstrument);

            if (audioNode.realInstrument != Instrument.INVALID)
            {
                amountWithInstrument += PlayerManager.PlayersWithInstrument(audioNode.realInstrument);
                amountLowered += audioLowering.GetCount(audioNode.realInstrument);
            }

            // Skip if no one is playing the instrument
            if (amountWithInstrument == 0)
            {
                return;
            }

            // Lower all volumes to a minimum of 5%
            float percent = 1f - (float) amountLowered / amountWithInstrument;
            foreach (var stem in audioNode.stems)
                GameManager.AudioManager.SetStemVolume(stem, percent * 0.95f + 0.05f);
        }

        public IEnumerator EndSong(bool showResultScreen)
        {
            // Dispose of all audio
            GameManager.AudioManager.Options.UseMinimumStemVolume = false;
            GameManager.AudioManager.SongEnd -= OnEndReached;
            GameManager.AudioManager.UnloadSong();

            // Call events
            OnSongEnd?.Invoke(Song);

            // Unpause just in case
            Time.timeScale = 1f;

            OnSongEnd?.Invoke(Song);

            // run animation + save if we've reached end of song
            if (showResultScreen)
            {
                yield return playCover
                    .DOFade(1f, 1f)
                    .WaitForCompletion();

                // save scores and destroy tracks
                foreach (var track in _tracks)
                {
                    track.SetPlayerScore();
                    Destroy(track.gameObject);
                }

                _tracks.Clear();
                // save MicPlayer score and destroy it
                if (MicPlayer.Instance != null)
                {
                    MicPlayer.Instance.SetPlayerScore();
                    Destroy(MicPlayer.Instance.gameObject);
                }

                // show play result screen; this is our main focus now
                playResultScreen.SetActive(true);
            }

            scoreDisplay.SetActive(false);
        }

        public void LowerAudio(Instrument ins)
        {
            audioLowering.Add(ins);
        }

        public void RaiseAudio(Instrument ins)
        {
            audioLowering.Remove(ins);
        }

        public void ReverbAudio(Instrument ins, bool apply)
        {
            if (apply)
            {
                audioReverb.Add(ins);
            }
            else
            {
                audioReverb.Remove(ins);
            }
        }

        public void TrackWhammyPitch(Instrument instrument, float delta, bool enable)
        {
            var stem = instrument switch
            {
                Instrument.GUITAR or Instrument.REAL_GUITAR => SongStem.Guitar,
                Instrument.BASS or Instrument.REAL_BASS     => SongStem.Bass,
                Instrument.RHYTHM                           => SongStem.Rhythm,
                _                                           => SongStem.Song
            };
            if (!audioPitchBend.TryGetValue(stem, out var current))
                return;

            // Accumulate delta
            // We take in a delta value to account for multiple players on the same part,
            // if we used absolute then there would be no way to prevent the pitch jittering
            // due to two players whammying at the same time
            current.percent += delta;
            current.enabled = enable;
            audioPitchBend[stem] = current;
        }

        private void UpdateWhammyPitch()
        {
            // Set pitch bend
            foreach (var (stem, current) in audioPitchBend)
            {
                float percent = current.enabled ? Mathf.Clamp(current.percent, 0f, 1f) : 0f;
                // The pitch is always set regardless of the enable state, seems like it prevents
                // issues with whammiable stems becoming muddy over time
                GameManager.AudioManager.SetWhammyPitch(stem, percent);
            }
        }

        public void Exit(bool toSongSelect = true)
        {
            if (!endReached)
            {
                endReached = true;
                StartCoroutine(EndSong(false));
            }

            MainMenu.showSongSelect = toSongSelect;
            GameManager.Instance.LoadScene(SceneIndex.MENU);
        }
    }
}