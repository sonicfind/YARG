using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;
using YARG.Settings;
using static YARG.PlayerManager;

namespace YARG.UI
{
    public class GenericLyricContainer : MonoBehaviour
    {
        private static List<GenericLyricInfo> LyricInfos => Play.Instance.chart.genericLyrics;

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        [Space]
        [SerializeField]
        private GameObject _normalBackground;

        [SerializeField]
        private GameObject _transparentBackground;

        private int _lyricIndex;
        private int _lyricPhraseIndex;

        private void Start()
        {
            _lyricText.text = string.Empty;

            // Set proper background
            switch (SettingsManager.Settings.LyricBackground.Data)
            {
                case "Normal":
                    _normalBackground.SetActive(true);
                    _transparentBackground.SetActive(false);
                    break;
                case "Transparent":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(true);
                    break;
                case "None":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(false);
                    break;
            }

            if (LyricInfos.Count <= 0 || players.Any(player => player.chosenInstrument is Instrument.VOCALS or Instrument.HARMONY))
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (_lyricIndex >= LyricInfos.Count)
            {
                return;
            }

            var lyric = LyricInfos[_lyricIndex];
            if (_lyricPhraseIndex >= lyric.lyric.Count && lyric.EndTime < Play.Instance.SongTime)
            {
                // Clear phrase
                _lyricText.text = string.Empty;

                _lyricPhraseIndex = 0;
                _lyricIndex++;
            }
            else if (_lyricPhraseIndex < lyric.lyric.Count &&
                lyric.lyric[_lyricPhraseIndex].time < Play.Instance.SongTime)
            {
                // Consolidate lyrics
                string o = "<color=#5CB9FF>";
                for (int i = 0; i < lyric.lyric.Count; i++)
                {
                    (_, string str) = lyric.lyric[i];

                    if (str.EndsWith("-"))
                    {
                        o += str[..^1].Replace("=", "-");
                    }
                    else
                    {
                        o += str.Replace("=", "-") + " ";
                    }

                    if (i + 1 > _lyricPhraseIndex)
                    {
                        o += "</color>";
                    }
                }

                _lyricText.text = o;
                _lyricPhraseIndex++;
            }
        }
    }
}