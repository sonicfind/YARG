using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.Input;
using YARG.Song;
using YARG.Song.Library;

namespace YARG
{
    public class LoadingManager : MonoBehaviour
    {
        public static LoadingManager Instance { get; private set; }

        [SerializeField]
        private TextMeshProUGUI loadingPhrase;

        [SerializeField]
        private TextMeshProUGUI subPhrase;

        private readonly Queue<Func<UniTask>> _loadQueue = new();

        private void Awake()
        {
            Instance = this;
        }

        private async UniTask Start()
        {
            Queue(async () => { await SongSources.LoadSources(i => SetLoadingText("Loading song sources...", i)); });

            // Fast scan (cache read) on startup
            QueueSongRefresh(true);

            Queue(async () =>
            {
                SetLoadingText("Reading scores...");
                await ScoreManager.FetchScores();
            });

            await StartLoad();
        }

        public async UniTask StartLoad()
        {
            if (_loadQueue.Count <= 0)
            {
                return;
            }

            gameObject.SetActive(true);

            while (_loadQueue.Count > 0)
            {
                var func = _loadQueue.Dequeue();
                await func();
            }

            gameObject.SetActive(false);

#if UNITY_EDITOR
            // Test Play stuff
            StartTestPlayMode();
#endif
        }

        public void Queue(Func<UniTask> func)
        {
            _loadQueue.Enqueue(func);
        }

        public void QueueSongRefresh(bool fast)
        {
            Queue(async () => { await ScanSongFolders(fast); });
        }

        private async UniTask ScanSongFolders(bool fast)
        {
            SetLoadingText("Loading songs...");
            await SongContainer.Scan(fast, UpdateSongUi);
        }

        private void SetLoadingText(string phrase, string sub = null)
        {
            loadingPhrase.text = phrase;
            subPhrase.text = sub;
        }

        private void UpdateSongUi(SongCache cache)
        {
            string phrase = string.Empty;
            string subText = null;
            switch (cache.Progress)
            {
                case ScanProgress.LoadingCache:
                    phrase = "Loading song cache...";
                    break;
                case ScanProgress.LoadingSongs:
                    phrase = "Loading songs...";
                    break;
                case ScanProgress.Sorting:
                    phrase = "Sorting songs...";
                    break;
                case ScanProgress.WritingCache:
                    phrase = "Writing song cache...";
                    break;
            }

            switch (cache.Progress)
            {
                case ScanProgress.LoadingCache:
                case ScanProgress.LoadingSongs:
                    subText = $"Folders Scanned: {cache.NumScannedDirectories}\n" +
                              $"Songs Scanned: {cache.Count}\n" +
                              $"Errors: {cache.BadSongCount}"; break;
            }
            SetLoadingText(phrase, subText);
        }

#if UNITY_EDITOR
        private void StartTestPlayMode()
        {
            var info = GameManager.Instance.TestPlayInfo;

            // Skip if not test play mode
            if (!info.TestPlayMode)
            {
                return;
            }

            info.TestPlayMode = false;

            // Add the bots
            if (!info.NoBotsMode)
            {
                AddTestPlayPlayer(new PlayerManager.Player
                {
                    chosenInstrument = Instrument.GUITAR,
                    chosenDifficulty = Difficulty.EXPERT,
                    inputStrategy = new FiveFretInputStrategy
                    {
                        BotMode = true
                    }
                });

                AddTestPlayPlayer(new PlayerManager.Player
                {
                    chosenInstrument = Instrument.REAL_DRUMS,
                    chosenDifficulty = Difficulty.EXPERT_PLUS,
                    inputStrategy = new DrumsInputStrategy
                    {
                        BotMode = true
                    }
                });

                AddTestPlayPlayer(new PlayerManager.Player
                {
                    chosenInstrument = Instrument.VOCALS,
                    chosenDifficulty = Difficulty.EXPERT,
                    inputStrategy = new MicInputStrategy
                    {
                        BotMode = true
                    }
                });
            }

            // Get the Test Play song by hash, and play it
            if (SongContainer.SongsByHash.TryGetValue(info.TestPlaySongHash, out var song))
            {
                GameManager.Instance.SelectedSong = song[0];
                GameManager.Instance.LoadScene(SceneIndex.PLAY);
            }
        }

        private static void AddTestPlayPlayer(PlayerManager.Player p)
        {
            PlayerManager.players.Add(p);
            p.inputStrategy.Enable();
        }
#endif
    }
}