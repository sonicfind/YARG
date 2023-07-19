using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using YARG.Data;
using YARG.Serialization;
using YARG.Song;
using YARG.Song.Entries;
using YARG.Song.Entries.TrackScan;
using YARG.Types;
using YARG.UI.MusicLibrary.ViewTypes;
using YARG.Util;

namespace YARG.UI.MusicLibrary
{
    public class Sidebar : MonoBehaviour
    {
        [SerializeField]
        private Transform _difficultyRingsTopContainer;

        [SerializeField]
        private Transform _difficultyRingsBottomContainer;

        [SerializeField]
        private TextMeshProUGUI _album;

        [SerializeField]
        private TextMeshProUGUI _source;

        [SerializeField]
        private TextMeshProUGUI _charter;

        [SerializeField]
        private TextMeshProUGUI _genre;

        [SerializeField]
        private TextMeshProUGUI _year;

        [SerializeField]
        private TextMeshProUGUI _length;

        [SerializeField]
        private RawImage _albumCover;

        [Space]
        [SerializeField]
        private GameObject difficultyRingPrefab;

        private readonly List<DifficultyRing> _difficultyRings = new();
        private CancellationTokenSource _cancellationToken;

        public void Init()
        {
            // Spawn 10 difficulty rings
            // for (int i = 0; i < 10; i++) {
            // 	var go = Instantiate(difficultyRingPrefab, _difficultyRingsContainer);
            // 	difficultyRings.Add(go.GetComponent<DifficultyRing>());
            // }
            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(difficultyRingPrefab, _difficultyRingsTopContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }

            for (int i = 0; i < 5; ++i)
            {
                var go = Instantiate(difficultyRingPrefab, _difficultyRingsBottomContainer);
                _difficultyRings.Add(go.GetComponent<DifficultyRing>());
            }
        }

        public async UniTask UpdateSidebar()
        {
            // Cancel album art
            if (_cancellationToken != null)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
                _cancellationToken = null;
            }

            if (SongSelection.Instance.ViewList.Count <= 0)
            {
                return;
            }

            var viewType = SongSelection.Instance.ViewList[SongSelection.Instance.SelectedIndex];

            if (viewType is CategoryViewType categoryViewType)
            {
                // Hide album art
                _albumCover.texture = null;
                _albumCover.color = Color.clear;
                _album.text = string.Empty;

                int sourceCount = categoryViewType.CountOf(i => i.Source);
                _source.text = $"{sourceCount} sources";

                int charterCount = categoryViewType.CountOf(i => i.Charter);
                _charter.text = $"{charterCount} charters";

                int genreCount = categoryViewType.CountOf(i => i.Genre);
                _genre.text = $"{genreCount} genres";

                _year.text = string.Empty;
                _length.text = string.Empty;
                HelpBar.Instance.SetInfoText(string.Empty);

                // Hide all difficulty rings
                foreach (var difficultyRing in _difficultyRings)
                {
                    difficultyRing.gameObject.SetActive(false);
                }

                return;
            }

            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            _album.text = songEntry.Album;
            _source.text = SongSources.SourceToGameName(songEntry.Source);
            _charter.text = songEntry.Charter;
            _genre.text = songEntry.Genre;
            _year.text = songEntry.Year;
            HelpBar.Instance.SetInfoText(
                RichTextUtils.StripRichTextTagsExclude(songEntry.LoadingPhrase, RichTextUtils.GOOD_TAGS));

            // Format and show length
            if (songEntry.SongLengthTimeSpan.Hours > 0)
            {
                _length.text = songEntry.SongLengthTimeSpan.ToString(@"h\:mm\:ss");
            }
            else
            {
                _length.text = songEntry.SongLengthTimeSpan.ToString(@"m\:ss");
            }

            UpdateDifficulties(songEntry);

            // Finally, update album cover
            await LoadAlbumCover();
        }

        private void UpdateDifficulties(SongEntry entry)
        {
            // Show all difficulty rings
            foreach (var difficultyRing in _difficultyRings)
            {
                difficultyRing.gameObject.SetActive(true);
            }

            /*

                Guitar               ; Bass               ; 4 or 5 lane ; Keys     ; Mic (dependent on mic count)
                Pro Guitar or Co-op  ; Pro Bass or Rhythm ; True Drums  ; Pro Keys ; Band

            */

            _difficultyRings[0].SetInfo("guitar", entry.GetValues(NoteTrackType.Lead));
            _difficultyRings[1].SetInfo("bass", entry.GetValues(NoteTrackType.Bass));

            // 5-lane or 4-lane
            if (entry.GetDrumType() == DrumType.FIVE_LANE)
            {
                _difficultyRings[2].SetInfo("ghDrums", entry.GetValues(NoteTrackType.Drums_5));
            }
            else
            {
                _difficultyRings[2].SetInfo("drums", entry.GetValues(NoteTrackType.Drums_4));
            }

            _difficultyRings[3].SetInfo("keys", entry.GetValues(NoteTrackType.Keys));

            if (entry.HasInstrument(Instrument.HARMONY))
            {
                _difficultyRings[4].SetInfo(
                    entry.VocalParts switch
                    {
                        2 => "twoVocals",
                        >= 3 => "harmVocals",
                        _ => "vocals"
                    },
                    entry.GetValues(NoteTrackType.Harmonies)
                );
            }
            else
            {
                _difficultyRings[4].SetInfo("vocals", entry.GetValues(NoteTrackType.Vocals));
            }

            // Protar or Co-op
            if (entry.HasInstrument(Instrument.REAL_GUITAR))
            {
                var values = entry.GetValues(NoteTrackType.ProGuitar_17);
                if (values.intensity == -1)
                    values = entry.GetValues(NoteTrackType.ProGuitar_22);
                _difficultyRings[5].SetInfo("realGuitar", values);
            }
            else
            {
                _difficultyRings[5].SetInfo("guitarCoop", entry.GetValues(NoteTrackType.Coop));
            }

            if (entry.HasInstrument(Instrument.REAL_BASS))
            {
                var values = entry.GetValues(NoteTrackType.ProBass_17);
                if (values.intensity == -1)
                    values = entry.GetValues(NoteTrackType.ProBass_22);
                _difficultyRings[6].SetInfo("realBass", values);
            }
            else
            {
                _difficultyRings[6].SetInfo("rhythm", entry.GetValues(NoteTrackType.Rhythm));
            }

            _difficultyRings[7].SetInfo("trueDrums", new ScanValues(-1));
            _difficultyRings[8].SetInfo("realKeys", entry.GetValues(NoteTrackType.ProKeys));
            _difficultyRings[9].SetInfo("band", new ScanValues(entry.BandDifficulty));
        }

        public async UniTask LoadAlbumCover()
        {
            // Dispose of the old texture (prevent memory leaks)
            if (_albumCover.texture != null)
            {
                // This might seem weird, but we are destroying the *texture*, not the UI image.
                Destroy(_albumCover.texture);
            }

            // Hide album art until loaded
            _albumCover.texture = null;
            _albumCover.color = Color.clear;

            _cancellationToken = new();

            var viewType = SongSelection.Instance.ViewList[SongSelection.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            if (songEntry is IniSongEntry)
            {
                string[] possiblePaths =
                {
                    "album.png", "album.jpg", "album.jpeg",
                };

                // Load album art from one of the paths
                foreach (string path in possiblePaths)
                {
                    string fullPath = Path.Combine(songEntry.Directory, path);
                    if (File.Exists(fullPath))
                    {
                        await LoadSongIniCover(fullPath);
                        break;
                    }
                }
            }
            else
            {
                await LoadRbConCover(songEntry as ConSongEntry);
            }
        }

        private async UniTask LoadSongIniCover(string filePath)
        {
            var texture = await TextureLoader.Load(filePath, _cancellationToken.Token);

            if (texture != null)
            {
                // Set album cover
                _albumCover.texture = texture;
                _albumCover.color = Color.white;
                _albumCover.uvRect = new Rect(0f, 0f, 1f, 1f);
            }
        }
#nullable enable
        private async UniTask LoadRbConCover(ConSongEntry entry)
        {
            Texture2D texture = null;
            try
            {
                var file = entry.LoadImgFile();
                if (file == null) return;

                texture = await XboxImageTextureGenerator.GetTexture(file, _cancellationToken.Token);

                _albumCover.texture = texture;
                _albumCover.color = Color.white;
                _albumCover.uvRect = new Rect(0f, 0f, 1f, -1f);
            }
            catch (OperationCanceledException)
            {
                // Dispose of the texture (prevent memory leaks)
                if (texture != null)
                {
                    // This might seem weird, but we are destroying the *texture*, not the UI image.
                    Destroy(texture);
                }
            }
        }

        public void PrimaryButtonClick()
        {
            var viewType = SongSelection.Instance.ViewList[SongSelection.Instance.SelectedIndex];
            viewType.PrimaryButtonClick();
        }

        public void SearchFilter(string type)
        {
            var viewType = SongSelection.Instance.ViewList[SongSelection.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongEntry;

            string value = type switch
            {
                "source"  => songEntry.Source.SortStr,
                "album"   => songEntry.Album.SortStr,
                "year"    => songEntry.Year,
                "charter" => songEntry.Charter.SortStr,
                "genre"   => songEntry.Genre.SortStr,
                _         => throw new Exception("Unreachable")
            };
            SongSelection.Instance.SetSearchInput($"{type}:{value}");
        }
    }
}