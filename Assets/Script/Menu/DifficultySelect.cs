using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Data;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI
{
    public class DifficultySelect : MonoBehaviour
    {
        private enum State
        {
            INSTRUMENT,
            DIFFICULTY,
        }

        [SerializeField]
        private GenericOption[] options;

        [SerializeField]
        private TextMeshProUGUI header;

        [SerializeField]
        private TMP_InputField speedInput;

        [SerializeField]
        private Toggle brutalModeCheckbox;

        private List<PlayerManager.Player> playersToConfigure = new(); // Used to more cleanly handle vocals players
        private int playerIndex;                                       // The current player index (not used for vocals)
        private Instrument[] instruments;
        private Difficulty[] difficulties;
        private State state;

        private int optionCount;
        private int selected;

        public delegate void InstrumentSelectionAction(PlayerManager.Player playerInfo);

        public static event InstrumentSelectionAction OnInstrumentSelection;

        private void Start()
        {
            foreach (var option in options)
            {
                option.MouseHoverEvent += HoverOption;
                option.MouseClickEvent += ClickOption;
            }
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Up", () => { MoveOption(-1); }),
                new NavigationScheme.Entry(MenuAction.Down, "Down", () => { MoveOption(1); }),
                new NavigationScheme.Entry(MenuAction.Confirm, "Confirm", () => { Next(); }),
                new NavigationScheme.Entry(MenuAction.Back, "Back", () => { MainMenu.Instance.ShowSongSelect(); })
            }, false));

            //Debug.Log(GameManager.Instance.SelectedSong.AvailableParts);

            playerIndex = 0;
            playersToConfigure.Clear();

            // See if there are any mics
            bool anyMics = false;
            for (int index = 0; index < PlayerManager.players.Count; index++)
            {
                var player = PlayerManager.players[index];
                if (player.inputStrategy is MicInputStrategy)
                {
                    playersToConfigure.Add(player);
                    anyMics = true;
                }
            }

            // Use first player otherwise
            if (playersToConfigure.Count < 1)
            {
                playersToConfigure.Add(PlayerManager.players[playerIndex]);
            }

            // Get player info
            var allowedInstruments = playersToConfigure[0].inputStrategy.GetAllowedInstruments();
            string headerText = playersToConfigure[0].DisplayName;
            if (anyMics)
            {
                headerText = "Options for All Vocals";
                brutalModeCheckbox.interactable = false;
                playerIndex = -1;
            }

            UpdateInstrument(headerText, allowedInstruments);
        }

        private void OnDestroy()
        {
            Navigator.Instance.PopScheme();

            foreach (var option in options)
            {
                option.MouseHoverEvent -= HoverOption;
                option.MouseClickEvent -= ClickOption;
            }
        }

        private void MoveOption(int i)
        {
            // Deselect old one
            options[selected].SetSelected(false);

            selected += i;

            if (selected < 0)
            {
                selected = optionCount - 1;
            }
            else if (selected >= optionCount)
            {
                selected = 0;
            }

            // Select new one
            options[selected].SetSelected(true);
        }

        private void HoverOption(GenericOption option)
        {
            // Deselect old one
            options[selected].SetSelected(false);

            selected = Array.IndexOf(options, option);

            // Slighty different than with the keyboard.
            // Don't need to bound the top. The bottom should stop and not roll over or go to an empty option.
            if (selected >= optionCount)
            {
                selected = optionCount - 1;
            }

            // Select new one
            options[selected].SetSelected(true);
        }

        private void ClickOption(GenericOption option)
        {
            Next();
        }

        public void Next()
        {
            if (state == State.INSTRUMENT)
            {
                if (selected >= instruments.Length)
                {
                    foreach (var player in playersToConfigure)
                    {
                        player.chosenInstrument = Instrument.INVALID;
                    }

                    IncreasePlayerIndex();
                }
                else
                {
                    var instrument = instruments[selected];
                    foreach (var player in playersToConfigure)
                    {
                        player.chosenInstrument = instrument;
                    }

                    UpdateDifficulty(instrument);
                }
            }
            else if (state == State.DIFFICULTY)
            {
                foreach (var player in playersToConfigure)
                {
                    player.chosenDifficulty = difficulties[selected];
                    OnInstrumentSelection?.Invoke(player);
                }

                IncreasePlayerIndex();
            }
        }

        private void IncreasePlayerIndex()
        {
            if (playerIndex != -1)
            {
                if (brutalModeCheckbox.isOn)
                {
                    PlayerManager.players[playerIndex].brutalMode = true;
                }
                else
                {
                    PlayerManager.players[playerIndex].brutalMode = false;
                }
            }
            else
            {
                brutalModeCheckbox.interactable = true;
            }

            brutalModeCheckbox.isOn = false;

            // Next non-mic player
            playerIndex++;
            while (playerIndex < PlayerManager.players.Count
                && PlayerManager.players[playerIndex].inputStrategy is MicInputStrategy)
            {
                playerIndex++;
            }

            if (playerIndex >= PlayerManager.players.Count)
            {
                Play.speed = float.Parse(speedInput.text, CultureInfo.InvariantCulture);
                if (Play.speed <= 0f)
                {
                    Play.speed = 1f;
                }

                // Play song
                GameManager.Instance.LoadScene(SceneIndex.PLAY);
            }
            else
            {
                var player = PlayerManager.players[playerIndex];
                playersToConfigure.Clear();
                playersToConfigure.Add(player);
                UpdateInstrument(player.DisplayName, player.inputStrategy.GetAllowedInstruments());
            }
        }

        private void UpdateInstrument(string headerText, Instrument[] allowedInstruments)
        {
            state = State.INSTRUMENT;

            // Header
            header.text = headerText;

            var availableInstruments = GameManager.Instance.SelectedSong.GetInstruments();

            // Force add pro drums and five lane
            if (availableInstruments.Contains(Instrument.DRUMS))
            {
                availableInstruments.Add(Instrument.GH_DRUMS);
            }
            else if (availableInstruments.Contains(Instrument.GH_DRUMS))
            {
                availableInstruments.Add(Instrument.DRUMS);
                availableInstruments.Add(Instrument.REAL_DRUMS);
            }

            // Filter out to only allowed instruments
            availableInstruments.RemoveAll(i => !allowedInstruments.Contains(i));

            optionCount = availableInstruments.Count + 1;

            // Add to options
            var ops = new string[availableInstruments.Count + 1];
            instruments = availableInstruments.ToArray();
            for (int i = 0; i < instruments.Length; i++)
            {
                ops[i] = instruments[i].ToLocalizedName();
            }

            ops[^1] = "Sit Out";

            // Set text and sprites
            for (int i = 0; i < options.Length; i++)
            {
                options[i].SetSelected(false);

                if (i < ops.Length)
                {
                    options[i].SetText(ops[i]);

                    if (i < instruments.Length)
                    {
                        var sprite = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{instruments[i].ToStringName()}]")
                            .WaitForCompletion();
                        options[i].SetImage(sprite);
                    }
                }
                else
                    options[i].SetText("");
            }

            // Select
            selected = 0;
            options[0].SetSelected(true);
        }

        private void UpdateDifficulty(Instrument ins)
        {
            state = State.DIFFICULTY;

            // Get the correct instrument
            if (ins == Instrument.REAL_DRUMS || ins == Instrument.GH_DRUMS)
            {
                ins = Instrument.DRUMS;
            }

            // Get the available difficulties
            List<Difficulty> availableDifficulties;
            if (ins == Instrument.VOCALS || ins == Instrument.HARMONY)
                availableDifficulties = new((Difficulty[])Enum.GetValues(typeof(Difficulty)));
            else
            {
                availableDifficulties = new List<Difficulty>();
                for (int i = 0; i < (int) Difficulty.EXPERT_PLUS; i++)
                {
                    if (GameManager.Instance.SelectedSong.HasPart(ins, i))
                        availableDifficulties.Add((Difficulty) i);
                }
            }

            optionCount = availableDifficulties.Count;
            difficulties = availableDifficulties.ToArray();
            var ops = new string[optionCount];

            for (int i = 0; i < optionCount; i++)
            {
                ops[i] = availableDifficulties[i] switch
                {
                    Difficulty.EASY        => "Easy",
                    Difficulty.MEDIUM      => "Medium",
                    Difficulty.HARD        => "Hard",
                    Difficulty.EXPERT      => "Expert",
                    Difficulty.EXPERT_PLUS => "Expert+",
                    _                      => "Unknown"
                };
            }

            for (int i = 0; i < 6; i++)
            {
                options[i].SetText("");
                options[i].SetSelected(false);

                if (i < ops.Length)
                {
                    options[i].SetText(ops[i]);
                }
            }

            selected = optionCount - 1;
            options[optionCount - 1].SetSelected(true);
        }
    }
}