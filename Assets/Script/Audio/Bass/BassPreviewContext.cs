using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Settings;
using YARG.Song;

namespace YARG {
	public class BassPreviewContext : IPreviewContext {

		public double PreviewStartTime { get; private set; }
		public double PreviewEndTime { get; private set; }

		private readonly IAudioManager _manager;

		private CancellationTokenSource _loopCanceller;
		private CancellationTokenSource _loadCanceller;

		private bool _loadingPreview;
		private SongEntry _nextSongToLoad;

		public BassPreviewContext(IAudioManager manager) {
			_manager = manager;
		}

		public async UniTask StartLooping() {
			// Cancel any other loops
			if (_loopCanceller != null && !_loopCanceller.IsCancellationRequested) {
				_loopCanceller.Cancel();
				_loopCanceller.Dispose();
			}

			// Since _loopCanceller is directly set to, we need a local copy of it
			_loopCanceller = new CancellationTokenSource();
			var localCanceller = _loopCanceller;

			try {
				while (!localCanceller.IsCancellationRequested) {
					await UniTask.WaitUntil(() =>
						_manager.CurrentPositionD >= PreviewEndTime ||
						_manager.CurrentPositionD >= _manager.AudioLengthD,
					cancellationToken: localCanceller.Token);

					localCanceller.Token.ThrowIfCancellationRequested();

					await _manager.FadeOut(localCanceller.Token);

					localCanceller.Token.ThrowIfCancellationRequested();

					_manager.SetPosition(PreviewStartTime);
					_manager.FadeIn(SettingsManager.Settings.PreviewVolume.Data);
				}
			} catch (OperationCanceledException) {
				// It's all good if we cancelled!
			} catch (Exception e) {
				Debug.LogError("An error has occurred while attempting to loop preview audio.");
				Debug.LogException(e);
			}
		}

		public async UniTask PlayPreview(SongEntry song) {
			// Skip if preview shouldn't be played
			if (song == null || Mathf.Approximately(SettingsManager.Settings.PreviewVolume.Data, 0f)) {
				return;
			}

			// If a preview is being loaded, WE DON'T want to mess with that process
			if (_loadingPreview) {
				if (_loadCanceller?.IsCancellationRequested ?? false) {
					_loadCanceller.Cancel();
					_loadCanceller.Dispose();
				}

				_nextSongToLoad = song;
				return;
			}

			// If something is playing, fade it out first
			await _manager.FadeOut(_loadCanceller?.Token ?? default);

			// Check if cancelled
			if (_loadCanceller?.IsCancellationRequested ?? false) {
				return;
			}

			// Stop current preview (also cancels other preview loads)
			StopPreview();

			// Since _loadCanceller is directly set to, we need a local copy of it
			_loadCanceller = new CancellationTokenSource();
			var localCanceller = _loadCanceller;

			// Wait for a 100 milliseconds to prevent spam loading (no one likes Music Library lag)
			await UniTask.Delay(TimeSpan.FromMilliseconds(150.0), ignoreTimeScale: true);

			// Check if cancelled
			if (localCanceller.IsCancellationRequested) {
				return;
			}

			// Load the song
			_loadingPreview = true;
			await UniTask.RunOnThreadPool(() => {
				if (song is ExtractedConSongEntry conSong) {
					_manager.LoadMogg(conSong, false, SongStem.Crowd);
				} else {
					_manager.LoadSong(AudioHelpers.GetSupportedStems(song.Location), false, SongStem.Crowd);
				}
			});
			_loadingPreview = false;

			// Finish here if cancelled
			if (localCanceller.IsCancellationRequested) {
				// If another song was requested during the load time, just load that
				if (_nextSongToLoad != null) {
					PlayPreview(_nextSongToLoad).Forget();
					_nextSongToLoad = null;
				}

				return;
			}

			// Set preview start and end times
			PreviewStartTime = song.PreviewStartTimeSpan.TotalSeconds;
			if (PreviewStartTime <= 0.0) {
				PreviewStartTime = 10.0;
			}
			PreviewEndTime = song.PreviewEndTimeSpan.TotalSeconds;
			if (PreviewEndTime <= 0.0) {
				PreviewEndTime = PreviewStartTime + Constants.PREVIEW_DURATION;
			}

			// Set the position of the preview
			_manager.SetPosition(PreviewStartTime);

			// Start the audio
			_manager.FadeIn(SettingsManager.Settings.PreviewVolume.Data);
			StartLooping().Forget();

			return;
		}

		public void StopPreview() {
			if (_manager.IsAudioLoaded) {
				_manager.UnloadSong();
			}

			if (!_loadCanceller?.IsCancellationRequested ?? false) {
				_loadCanceller.Cancel();
				_loadCanceller.Dispose();
			}

			if (!_loopCanceller?.IsCancellationRequested ?? false) {
				_loopCanceller.Cancel();
				_loopCanceller.Dispose();
			}
		}

		public void Dispose() {
			StopPreview();
		}
	}
}