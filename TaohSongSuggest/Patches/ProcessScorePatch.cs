using HarmonyLib;
using SmartSongSuggest.UI;
using SongLibraryNS;
using System.Collections.Generic;

namespace SmartSongSuggest.Patches
{
    [HarmonyPatch(typeof(LevelCompletionResultsHelper), nameof(LevelCompletionResultsHelper.ProcessScore))]
    static class ProcessScorePatch
    {
        static void Postfix(in BeatmapKey beatmapKey, PlayerData playerData, PlayerLevelStatsData playerLevelStats, LevelCompletionResults levelCompletionResults, IReadonlyBeatmapData transformedBeatmapData, PlatformLeaderboardsModel platformLeaderboardsModel)
        {
            if (Managers.SongSuggestManager.toolBox == null || !beatmapKey.levelId.StartsWith("custom_level_")) return;
            Managers.SongSuggestManager.toolBox.log?.WriteLine($"Processing Result Screen");
            //if (!SettingsController.cfgInstance.RecordLocalScores) return;
            
            //We respect score submissions being turned off
            if (BS_Utils.Gameplay.ScoreSubmission.Disabled) return;
            
            //A fail is a fail
            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Failed) return;

            float maxScore = ScoreModel.ComputeMaxMultipliedScoreForBeatmap(transformedBeatmapData);
            int modifiedScore = levelCompletionResults.modifiedScore;
            int multipliedScore = levelCompletionResults.multipliedScore;

            //Avoid Lightshow maps with 0 notes, as it can give divice by 0 for acc. One should not record lightshows, one should enjoy them.
            if (modifiedScore == 0 || maxScore == 0) return;

            //Played hit 0 energy and failed during gameplay. This is leaderboard specific handling, for Song Suggest locals we only use songs that was completed without triggering NoFail.
            if (levelCompletionResults.energy == 0) return;
            
            //??Is this a check for speed FS/SF, or level failed with NoFail turned on??            
            //if (modifiedScore > multipliedScore) return;

            //Multiplied is "base score" and "modified score" is including modifiers, we record base acc, and further modifications should be done "elsewhere".
            float acc = multipliedScore / maxScore;

            string mapType = playerLevelStats.beatmapCharacteristic.serializedName;
            string mapId = beatmapKey.levelId.Substring(13).Split('_')[0];
            string difficulty = beatmapKey.difficulty.SerializedName();
            var songID = SongLibrary.GetID(mapType, difficulty, mapId);

            //Mod can only calculate with Normal speed for now.
            string speed = $"{levelCompletionResults.gameplayModifiers.songSpeed}";
            if (speed != "Normal") return;

            var m = levelCompletionResults.gameplayModifiers;
            var defaults = GameplayModifiers.noModifiers;
            var modifiers = new List<string>();
            if (m.noFailOn0Energy) modifiers.Add("NF");
            if (m.instaFail) modifiers.Add("IF");
            if (m.failOnSaberClash) modifiers.Add("SABER_CLASH");
            if (m.enabledObstacleType != defaults.enabledObstacleType) modifiers.Add("NO");
            if (m.fastNotes) modifiers.Add("FAST_NOTES");
            if (m.strictAngles) modifiers.Add("SA");
            if (m.disappearingArrows) modifiers.Add("DA");
            if (m.ghostNotes) modifiers.Add("GN");
            if (m.noBombs) modifiers.Add("NB");
            if (m.noArrows) modifiers.Add("NA");
            if (m.proMode) modifiers.Add("PM");
            if (m.zenMode) modifiers.Add("ZEN");
            if (m.smallCubes) modifiers.Add("SC");
            if (m.energyType != defaults.energyType) modifiers.Add(m.energyType.ToString() == "Battery" ? "BE" : "ENERGY_TYPE");
            string modifierText = string.Join(",", modifiers);
            //Store Session Score
            Managers.SongSuggestManager.toolBox.AddSessionScore(songID, acc, modifierText);

            //Verify that recording is active)
            if (SettingsController.cfgInstance.RecordLocalScores) Managers.SongSuggestManager.toolBox.AddLocalScore(songID, acc, modifierText);
            
            //Update the RankPlate as data changed.
            LevelDetailViewController.persController.RankPlateChanged();
        }
    }
}
