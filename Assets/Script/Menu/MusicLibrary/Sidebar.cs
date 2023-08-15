﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Persistent;
using YARG.Serialization;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
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
        private ViewType _currentView;

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
            if (MusicLibraryMenu.Instance.ViewList.Count <= 0)
            {
                return;
            }

            var viewType = MusicLibraryMenu.Instance.CurrentSelection;
            if (_currentView != null && _currentView == viewType)
                return;

            _currentView = viewType;

            // Cancel album art
            if (_cancellationToken != null)
            {
                _cancellationToken.Cancel();
                _cancellationToken.Dispose();
                _cancellationToken = null;
            }

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

            var songEntry = songViewType.SongMetadata;

            _album.text = songEntry.Album;
            _source.text = SongSources.SourceToGameName(songEntry.Source);
            _charter.text = songEntry.Charter;
            _genre.text = songEntry.Genre;
            _year.text = songEntry.Year;
            HelpBar.Instance.SetInfoText(
                RichTextUtils.StripRichTextTagsExclude(songEntry.LoadingPhrase, RichTextUtils.GOOD_TAGS));

            // Format and show length
            var time = TimeSpan.FromMilliseconds(songEntry.SongLength);
            if (time.Hours > 0)
            {
                _length.text = time.ToString(@"h\:mm\:ss");
            }
            else
            {
                _length.text = time.ToString(@"m\:ss");
            }

            UpdateDifficulties(songEntry.Parts);

            // Finally, update album cover
            await LoadAlbumCover();
        }

        private void UpdateDifficulties(AvailableParts parts)
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


            _difficultyRings[0].SetInfo("guitar", "FiveFretGuitar", parts.GetValues(Instrument.FiveFretGuitar));
            _difficultyRings[1].SetInfo("bass", "FiveFretBass", parts.GetValues(Instrument.FiveFretBass));

            // 5-lane or 4-lane
            if (parts.GetDrumType() == DrumsType.FiveLane)
            {
                _difficultyRings[2].SetInfo("ghDrums", "FiveLaneDrums", parts.GetValues(Instrument.FiveLaneDrums));
            }
            else
            {
                _difficultyRings[2].SetInfo("drums", "FourLaneDrums", parts.GetValues(Instrument.FourLaneDrums));
            }

            _difficultyRings[3].SetInfo("keys", "Keys", parts.GetValues(Instrument.Keys));

            if (parts.HasInstrument(Instrument.Harmony))
            {
                _difficultyRings[4].SetInfo(
                    parts.VocalsCount switch
                    {
                        2 => "twoVocals",
                        >= 3 => "harmVocals",
                        _ => "vocals"
                    },
                    "Harmony",
                    parts.GetValues(Instrument.Harmony)
                );
            }
            else
            {
                _difficultyRings[4].SetInfo("vocals", "Vocals", parts.GetValues(Instrument.Vocals));
            }

            // Protar or Co-op
            if (parts.HasInstrument(Instrument.ProGuitar_17Fret) || parts.HasInstrument(Instrument.ProGuitar_22Fret))
            {
                var values = parts.GetValues(Instrument.ProGuitar_17Fret);
                if (values.intensity == -1)
                    values = parts.GetValues(Instrument.ProGuitar_22Fret);
                _difficultyRings[5].SetInfo("realGuitar", "ProGuitar", values);
            }
            else
            {
                _difficultyRings[5].SetInfo("guitarCoop", "FiveFretCoopGuitar", parts.GetValues(Instrument.FiveFretCoopGuitar));
            }

            // ProBass or Rhythm
            if (parts.HasInstrument(Instrument.ProBass_17Fret) || parts.HasInstrument(Instrument.ProBass_22Fret))
            {
                var values = parts.GetValues(Instrument.ProBass_17Fret);
                if (values.intensity == -1)
                    values = parts.GetValues(Instrument.ProBass_22Fret);
                _difficultyRings[6].SetInfo("realBass", "ProBass", values);
            }
            else
            {
                _difficultyRings[6].SetInfo("rhythm", "FiveFretRhythm", parts.GetValues(Instrument.FiveFretRhythm));
            }

            _difficultyRings[7].SetInfo("trueDrums", "TrueDrums", new PartValues(-1));
            _difficultyRings[8].SetInfo("realKeys", "ProKeys", parts.GetValues(Instrument.ProKeys));
            _difficultyRings[9].SetInfo("band", "Band", parts.GetValues(Instrument.Band));
        }

        public async UniTask LoadAlbumCover()
        {
            _cancellationToken = new();

            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            // Dispose of the old texture (prevent memory leaks)
            // This might seem where, but we're *destroying the texture*, not the raw image component
            if (_albumCover.texture != null)
            {
                Destroy(_albumCover.texture);
            }

            // Load the new one
            await songViewType.SongMetadata.SetRawImageToAlbumCover(_albumCover, _cancellationToken.Token);
        }

        public void PrimaryButtonClick()
        {
            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            viewType.PrimaryButtonClick();
        }

        public void SearchFilter(string type)
        {
            var viewType = MusicLibraryMenu.Instance.ViewList[MusicLibraryMenu.Instance.SelectedIndex];
            if (viewType is not SongViewType songViewType)
            {
                return;
            }

            var songEntry = songViewType.SongMetadata;

            string value = type switch
            {
                "source"  => songEntry.Source,
                "album"   => songEntry.Album,
                "year"    => songEntry.Year,
                "charter" => songEntry.Charter,
                "genre"   => songEntry.Genre,
                _         => throw new Exception("Unreachable")
            };
            MusicLibraryMenu.Instance.SetSearchInput($"{type}:{value}");
        }
    }
}