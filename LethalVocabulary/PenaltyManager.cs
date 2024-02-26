using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using Unity.Netcode;
using Random = UnityEngine.Random;

namespace LethalVocabulary;

public class PenaltyManager : NetworkBehaviour {
    public static PenaltyManager Instance;

    public bool roundInProgress;
    public bool hideSharedCategories;
    public bool hidePrivateCategories;
    public bool punishCurseWords;
    public readonly HashSet<string> PrivateCategories = new();
    public readonly HashSet<string> PrivateWords = new();
    public readonly HashSet<string> SharedCategories = new();
    public readonly HashSet<string> SharedWords = new();
    private Grammar _allBannedWords;

    private Dictionary<string, HashSet<string>> _categories;

    private void Awake () {
        Instance = this;
        _categories = Config.GetAllCategories();
        hideSharedCategories = Config.HideSharedCategories.Value;
        hidePrivateCategories = Config.HidePrivateCategories.Value;
        punishCurseWords = Config.PunishCurseWords.Value;
    }

    // ------------------
    // Player Punishments
    // ------------------
    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId) {
        PunishPlayerClientRpc(clientId);
    }

    [ClientRpc]
    public void PunishPlayerClientRpc (ulong clientId) {
        var player = StartOfRound.Instance.allPlayerObjects[clientId];
        Landmine.SpawnExplosion(player.transform.position, true, 1, 0);
    }

    // ---------------------
    // Start Round Functions
    // ---------------------
    [ServerRpc]
    public void SetRoundInProgressServerRpc (bool value) {
        var sharedCategoryWords = PickCategory(Config.SharedCategoriesPerMoon.Value);
        AddSharedCategoriesClientRpc(string.Join(",", sharedCategoryWords[0]),
            string.Join(",", sharedCategoryWords[1]));
        AddPrivateCategoriesClientRpc(Config.PrivateCategoriesPerMoon.Value);

        SetRoundInProgressClientRpc(value);
    }

    [ClientRpc]
    public void SetRoundInProgressClientRpc (bool value) {
        roundInProgress = value;

        if (roundInProgress) {
            // Create category hints
            string categoryHints = WriteHints();
            if (categoryHints.Length > 0) Plugin.DisplayHUDTip("Don't talk about...", categoryHints, false);

            // Add words to and start the recognizer
            var words = new HashSet<string>();
            words.UnionWith(SharedWords);
            words.UnionWith(PrivateWords);
            _allBannedWords = SpeechRecognizer.CreateGrammar(words, "category", 1);
            Plugin.Instance.SpeechRecognizer.AddGrammar(_allBannedWords);
            Plugin.Instance.SpeechRecognizer.Start();
        }
        else {
            SharedCategories.Clear();
            SharedWords.Clear();
            SharedWords.Clear();
            PrivateCategories.Clear();
            PrivateWords.Clear();
            Plugin.Instance.SpeechRecognizer.Stop();
            Plugin.Instance.SpeechRecognizer.UnloadGrammar(_allBannedWords);
        }
    }

    [ClientRpc]
    public void AddSharedCategoriesClientRpc (string sharedCategories, string sharedWords) {
        SharedCategories.UnionWith(Plugin.ReadSeparatedString(sharedCategories, false));
        SharedWords.UnionWith(Plugin.ReadSeparatedString(sharedWords, false));
    }

    [ClientRpc]
    public void AddPrivateCategoriesClientRpc (int amount) {
        var categoryWords = PickCategory(amount);
        PrivateCategories.UnionWith(categoryWords[0]);
        PrivateWords.UnionWith(categoryWords[1]);
    }

    // -----------------------
    // Toggle PunishCurseWords
    // -----------------------
    [ServerRpc]
    public void TogglePunishCurseWordsServerRpc () {
        punishCurseWords = !punishCurseWords;
        SetPunishCurseWordsClientRpc(punishCurseWords);
    }

    [ClientRpc]
    public void SetPunishCurseWordsClientRpc (bool value) {
        if (!IsHost) punishCurseWords = value;
        if (punishCurseWords)
            Plugin.DisplayHUDTip("Curse words are banned!", "Watch your language...", false);
        else
            Plugin.DisplayHUDTip("Curse words are legal!", "Go wild ;)", false);
    }

    // ---------------------
    // Sync current settings
    // ---------------------
    [ServerRpc(RequireOwnership = false)]
    public void RequestCurrentSettingsServerRpc (ulong syncClientId) {
        SendCurrentSettingsClientRpc(syncClientId, roundInProgress, punishCurseWords, hideSharedCategories,
            hidePrivateCategories);
    }

    [ClientRpc]
    public void SendCurrentSettingsClientRpc (ulong syncClientId, bool setRip, bool setPcw, bool setHsc, bool setHpc) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != syncClientId) return;
        roundInProgress = setRip;
        punishCurseWords = setPcw;
        hideSharedCategories = setHsc;
        hidePrivateCategories = setHpc;
        Plugin.logger.LogInfo(
            $"Received settings from host! (RoundInProgress={roundInProgress}, PunishCurseWords={punishCurseWords}, " +
            $"HideSharedCategories={hideSharedCategories}, HidePrivateCategories={hidePrivateCategories})");
    }

    // --------------
    // Helper Methods
    // --------------
    public bool StringIsLegal (string text) {
        text = text.ToLower();
        if (!roundInProgress) return true;
        if (Plugin.StringHasWords(text, SharedWords) || Plugin.StringHasWords(text, PrivateWords)) return false;
        if (punishCurseWords && Plugin.StringHasWords(text, SpeechRecognizer.DefaultCurseWordsSet)) return false;
        return true;
    }

    private HashSet<string>[] PickCategory (int amount, bool avoidActiveCategories = true) {
        // Prepare the list of available categories
        var spawnableEnemies = EnemyHelper.GetEnemiesFromLevel(StartOfRound.Instance.currentLevel);
        HashSet<string> availableCategories = new();

        availableCategories.UnionWith(spawnableEnemies
            .Select(enemy => EnemyHelper.MappedEnemyNames[enemy.enemyType.enemyName])
            .Where(mappedName => mappedName != null));

        if (avoidActiveCategories) {
            availableCategories.RemoveWhere(category => SharedCategories.Contains(category));
            availableCategories.RemoveWhere(category => PrivateCategories.Contains(category));
        }

        // Select categories from the set of available categories
        HashSet<string> selectedCategories = new();
        HashSet<string> selectedWords = new();
        amount = Math.Max(0, amount);

        for (int i = 0; i < Math.Min(amount, availableCategories.Count); i++) {
            int index = Random.RandomRangeInt(0, availableCategories.Count);
            string category = availableCategories.ElementAt(index);

            selectedCategories.Add(category);
            selectedWords.UnionWith(_categories[category]);
            availableCategories.Remove(category);
        }

        return new[] { selectedCategories, selectedWords };
    }

    public string WriteHints () {
        string categoryHints = "";
        if (!hideSharedCategories)
            categoryHints += string.Join(", ", SharedCategories);
        if (!hidePrivateCategories)
            categoryHints += (categoryHints.Length > 0 ? ", " : "") + string.Join(", ", PrivateCategories);
        return categoryHints;
    }
}