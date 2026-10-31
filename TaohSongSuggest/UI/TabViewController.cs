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
            // GameplaySetup may parse the tab only after AddTab has returned.
            while (statusComponent == null)
                await Task.Delay(100);

            while (SongSuggestManager.toolBox == null && !SongSuggestManager.InitializationFailed)
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

        private int loadingDots;
        private bool statusUpdateErrorLogged;
        public void RefreshProgressBar(float prog)
        {
            try
            {
                if (statusComponent == null) return;
                statusComponent.raycastTarget = false;
                statusComponent.enableAutoSizing = true;
                statusComponent.fontSizeMin = 1.5f;
                statusComponent.fontSizeMax = 4;
                if (SongSuggestManager.toolBox == null && !SongSuggestManager.InitializationFailed)
                {
                    loadingDots = loadingDots % 3 + 1;
                    statusComponent.text = "Loading" + new string('.', loadingDots) +
                        " " + SongSuggestManager.InitializationStatus.Replace('\n', ' ').Replace('\r', ' ');
                }
                else
                {
                    statusComponent.text = SongSuggestManager.InitializationFailed ? SongSuggestManager.InitializationStatus :
                        SongSuggestManager.toolBox.status;
                }

                if (bgProgress == null) return;
                bgProgress.raycastTarget = false;
                bgProgress.color = Color.green;
                if (SongSuggestManager.InitializationFailed) bgProgress.color = Color.red;
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
            catch (Exception ex)
            {
                if (!statusUpdateErrorLogged)
                {
                    statusUpdateErrorLogged = true;
                    Plugin.Log.Error("Could not update SmartSongSuggest tab status: " + ex);
                }
            }

        }
    }
}
