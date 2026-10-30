using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Util;
using BeatSaberMarkupLanguage.ViewControllers;
using BeatSaberPlaylistsLib.Types;
using HMUI;
using Settings;
using SmartSongSuggest.Configuration;
using SmartSongSuggest.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

namespace SmartSongSuggest.UI
{
    internal class TabViewController : NotifiableSingleton<TabViewController>
    {
        [UIComponent("bgProgress")]
        public ImageView bgProgress;

        [UIComponent("statusText")]
        public TextMeshProUGUI statusComponent;

        [UIValue("loaded")]
        public bool loaded
        {
            get => SongSuggestManager.toolBox != null;
        }

        public async void Initialize()
        {
            while (SongSuggestManager.toolBox == null)
            {
                RefreshProgressBar(0);
                await Task.Delay(200);
            }

            RefreshProgressBar(0);

            NotifyPropertyChanged(nameof(loaded));
        }

        private int generationBusy;
        private string generationError;

        [UIAction("RegenSuggest")]
        public void RegenSuggest() => RunGeneration(false);

        [UIAction("RegenOldest")]
        public void RegenOldNew() => RunGeneration(true);

        private async void RunGeneration(bool oldAndNew)
        {
            if (Interlocked.CompareExchange(ref generationBusy, 1, 0) != 0) return;
            generationError = null;
            try
            {
                var core = SongSuggestManager.toolBox;
                if (core == null) throw new InvalidOperationException("SongSuggestCore is not ready.");
                var cfg = SettingsController.cfgInstance;
                var suggestSettings = oldAndNew ? null : SongSuggestManager.GetSongSuggestSettingsOld(cfg);
                var oldSettings = oldAndNew ? SongSuggestManager.GetOldAndNewSettings(cfg) : null;
                UpdateProgessNew();
                await Task.Run(() =>
                {
                    if (oldAndNew) core.GenerateOldestSongs(oldSettings);
                    else core.GenerateSongSuggestions(suggestSettings);
                });
                IPlaylist playlist = SongSuggestManager.UpdatePlaylists(oldAndNew ? "Old and New" : "Song Suggest");
                if (playlist != null)
                {
                    var navigation = GameObject.FindObjectOfType<LevelFilteringNavigationController>();
                    navigation?.SelectAnnotatedBeatmapLevelCollection(playlist.PlaylistLevelPack);
                }
            }
            catch (Exception ex)
            {
                generationError = ex.Message;
                Plugin.Log.Error(ex);
            }
            finally
            {
                Interlocked.Exchange(ref generationBusy, 0);
                RefreshProgressBar(0);
            }
        }

        [UIAction("Settings")]
        public void Settings()
        {
            UIManager.ShowFlow();
        }

        public async void UpdateProgessNew()
        {
            while (Volatile.Read(ref generationBusy) != 0)
            {
                RefreshProgressBar(SongSuggestManager.toolBox.songSuggest != null ? (float)SongSuggestManager.toolBox.songSuggest.songSuggestCompletion : 0);
                await Task.Delay(200);
            }

            RefreshProgressBar(0);
        }

        int dots = 0;
        public void RefreshProgressBar(float prog)
        {
            try
            {
                dots = (dots % 3) + 1;
                statusComponent.text = SongSuggestManager.toolBox != null ? SongSuggestManager.toolBox.status : "Loading" + new string('.', dots);

                bgProgress.color = Color.green;
                if (generationError != null)
                {
                    statusComponent.text = generationError;
                    bgProgress.color = Color.red;
                }

                //Core was not placed in libs, lets inform user of this
                if (!SongSuggestManager.coreDetected)
                {
                    statusComponent.text = "No Core Found, make sure SongSuggestCore.dll is placed in the Libs folder";
                    bgProgress.color = Color.red;
                }

                //Core and UI version do not match
                if (!SongSuggestManager.coreVersionValidated)
                {
                    statusComponent.text = "SongSuggestCore.dll is of an unsupported version";
                    bgProgress.color = Color.red;
                }

                var x = (bgProgress.gameObject.transform as RectTransform);
                if (x == null)
                    return;

                x.anchorMax = new Vector2(prog, 1);

                x.ForceUpdateRectTransforms();
            }
            catch
            {

            }

        }
    }
}
