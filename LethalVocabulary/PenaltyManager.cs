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

    #region Player Punishment Rpcs
    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId) {
        PunishPlayerClientRpc(clientId);
    }

    [ClientRpc]
    public void PunishPlayerClientRpc (ulong clientId) {
        var player = StartOfRound.Instance.allPlayerObjects[clientId];
        Landmine.SpawnExplosion(player.transform.position, true, 1, 0);
    }
    #endregion

    #region Start Round Rpcs
    [ServerRpc]
    public void SetRoundInProgressServerRpc (bool value) {
        var sharedCategoryWords = PickCategory(Config.SharedCategoriesPerMoon.Value);
        string sharedCategories = string.Join(",", sharedCategoryWords[0]);
        string sharedWords = string.Join(",", sharedCategoryWords[1]);

        if (!StartOfRound.Instance.currentLevel.PlanetName.Equals("71 Gordion")) {
            AddSharedCategoriesClientRpc(sharedCategories, sharedWords);
            AddPrivateCategoriesClientRpc(Config.PrivateCategoriesPerMoon.Value);
        }
        
        SetRoundInProgressClientRpc(value);
    }

    [ClientRpc]
    public void SetRoundInProgressClientRpc (bool value) {
        roundInProgress = value;

        if (roundInProgress) {
            // Add words to and start the recognizer
            var words = new HashSet<string>();
            words.UnionWith(SharedWords);
            words.UnionWith(PrivateWords);
            if (punishCurseWords) words.UnionWith(SpeechRecognizer.DefaultCurseWordsSet);
            _allBannedWords = new(SpeechRecognizer.CreateSrgs(words)) {
                Name = "category", Priority = 1
            };
            
            Plugin.Instance.SpeechRecognizer.LoadAndStart(_allBannedWords);
            DisplayCategoryHints();
        }
        else {
            SharedCategories.Clear();
            SharedWords.Clear();
            SharedWords.Clear();
            PrivateCategories.Clear();
            PrivateWords.Clear();
            Plugin.Instance.SpeechRecognizer.StopAndUnload(_allBannedWords);
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
    #endregion

    #region Change Game Setting Rpcs
    [ServerRpc]
    public void TogglePunishCurseWordsServerRpc () {
        punishCurseWords = !punishCurseWords;
        SetPunishCurseWordsClientRpc(punishCurseWords);
    }

    [ClientRpc]
    public void SetPunishCurseWordsClientRpc (bool value) {
        if (!IsHost) punishCurseWords = value;
        if (punishCurseWords)
            Plugin.DisplayHUDTip("Curse words will be banned!", "Applies on the next moon\nWatch your language...");
        else
            Plugin.DisplayHUDTip("Curse words will be legal!", "Applies on the next moon\nGo wild ;)");
    }
    #endregion

    #region Sync Game Settings Rpcs
    [ServerRpc(RequireOwnership = false)]
    public void RequestCurrentSettingsServerRpc (ulong syncClientId) {
        SendCurrentSettingsClientRpc(syncClientId, punishCurseWords, hideSharedCategories,
            hidePrivateCategories);
    }

    [ClientRpc]
    public void SendCurrentSettingsClientRpc (ulong syncClientId, bool setPcw, bool setHsc, bool setHpc) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != syncClientId) return;
        punishCurseWords = setPcw;
        hideSharedCategories = setHsc;
        hidePrivateCategories = setHpc;
        Plugin.logger.LogInfo(
            $"Received settings from host:\nPunishCurseWords={punishCurseWords}\n" +
            $"HideSharedCategories={hideSharedCategories}\nHidePrivateCategories={hidePrivateCategories}");
    }
    #endregion

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

    public void DisplayCategoryHints () {
        string categoryHints = "";
        if (!hideSharedCategories)
            categoryHints += string.Join(", ", SharedCategories);
        if (!hidePrivateCategories)
            categoryHints += (categoryHints.Length > 0 ? ", " : "") + string.Join(", ", PrivateCategories);
        
        if (categoryHints.Length > 0) Plugin.DisplayHUDTip("Don't talk about...", categoryHints);
    }
}