using System.Globalization;
using SFB;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.UI {
	public class SettingsMenu : MonoBehaviour {
		[SerializeField]
		private TMP_InputField songFolderInput;
		[SerializeField]
		private TMP_InputField calibrationInput;
		[SerializeField]
		private Toggle lowQualityToggle;
		[SerializeField]
		private Toggle karaokeToggle;
		[SerializeField]
		private Toggle showHitWindowToggle;
		[SerializeField]
		private TMP_InputField ipInput;

		[SerializeField]
		private GameObject joinServerButton;

		private void Start() {
			songFolderInput.text = SongLibrary.songFolder.FullName;
			calibrationInput.text = PlayerManager.globalCalibration.ToString();
			lowQualityToggle.isOn = GameManager.Instance.LowQualityMode;
			karaokeToggle.isOn = GameManager.Instance.KaraokeMode;
			showHitWindowToggle.isOn = GameManager.Instance.showHitWindow;

			if (GameManager.client != null) {
				joinServerButton.SetActive(false);
			}
		}

		public void BrowseSongFolder() {
			StandaloneFileBrowser.OpenFolderPanelAsync("Choose Folder", null, false, folder => {
				songFolderInput.text = folder[0];
				SongFolderUpdate();
			});
		}

		public void SongFolderUpdate() {
			SongLibrary.songFolder = new(songFolderInput.text);
			PlayerPrefs.SetString("songFolder", songFolderInput.text);

			SongLibrary.Reset();
		}

		public void CalibrationUpdate() {
			// Guaranteed to as the input field is decimal
			PlayerManager.globalCalibration = float.Parse(calibrationInput.text, CultureInfo.InvariantCulture);
		}

		public void LowQualityUpdate() {
			GameManager.Instance.LowQualityMode = lowQualityToggle.isOn;
		}

		public void KaraokeModeUpdate() {
			GameManager.Instance.KaraokeMode = karaokeToggle.isOn;
		}

		public void ShowHitWindowUpdate() {
			GameManager.Instance.showHitWindow = showHitWindowToggle.isOn;
		}

		public void JoinServer() {
			GameManager.client = new();
			GameManager.client.Start(ipInput.text);

			joinServerButton.SetActive(false);
		}
	}
}