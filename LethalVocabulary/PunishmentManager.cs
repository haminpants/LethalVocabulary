using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

// ReSharper disable Unity.PerformanceCriticalCodeInvocation

namespace LethalVocabulary;

public class PunishmentManager : NetworkBehaviour {
    public const int CategoryNameMaxLength = 64;
    public const int CategoryWordsMaxLength = 512;
    public const int WordMaxLength = 128;

    public static PunishmentManager Instance;

    public readonly Dictionary<string, HashSet<string>> Categories = new();
    public readonly HashSet<string> ActiveCategories = new();
    public readonly HashSet<string> ActiveWords = new();

    public readonly NetworkVariable<bool> DisplayCategoryHints = new();
    public readonly NetworkVariable<bool> MoonInProgress = new();
    
    private static GameObject _landminePrefab;
    private static ShipTeleporter _teleporter;

    public PunishmentManager () {
        Instance = this;
        MoonInProgress.OnValueChanged += OnMoonInProgressChanged;
        DisplayCategoryHints.OnValueChanged += OnDisplayCategoryHintsChanged;
    }

    public bool StringIsLegal (string message, double confidence = 1) {
        message = message.Trim().ToLower();
        PlayerControllerB player = StartOfRound.Instance.localPlayerController;

        if (!MoonInProgress.Value || player.isPlayerDead || confidence < Config.ConfidenceThreshold.Value) return true;

        foreach (string word in ActiveWords) {
            if (!message.Contains(word)) continue;
            PunishPlayerServerRpc(player.playerClientId);
            return false;
        }

        return true;
    }
    
    public void LoadConfig () {
        Plugin.Console.LogInfo("Loading config...");
        foreach (KeyValuePair<Category, string> entry in CategoryHelper.CategoryToConfigWords)
            AddCategory(entry.Key, entry.Value);

        // TODO: load custom words

        DisplayCategoryHints.Value = Config.DisplayCategoryHints.Value;
    }

    public void LoadGameResources () {
        Plugin.Console.LogInfo("Loading game resources...");
        foreach (SpawnableMapObject so in RoundManager.Instance.spawnableMapObjects)
            if (so.prefabToSpawn.GetComponentInChildren<Landmine>())
                _landminePrefab = so.prefabToSpawn;

        _teleporter = Resources.FindObjectsOfTypeAll<GameObject>().First(o => o.gameObject.name.Contains("Inverse"))
            .GetComponent<ShipTeleporter>();

        if (_landminePrefab == null) Plugin.Console.LogError("Failed to locate Landmine resource");
        if (_teleporter == null) Plugin.Console.LogError("Failed to locate Inverse Teleporter resource");
    }

    #region Punishments

    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId) {
        TeleportPunishmentClientRpc(clientId);
    }

    [ClientRpc]
    public void ExplodePunishmentClientRpc (ulong clientId) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        GameObject landmine = Instantiate(_landminePrefab, player.transform.position, Quaternion.identity);
        landmine.GetComponent<Landmine>().ExplodeMineClientRpc();
        // TODO: improve this
    }

    #region Teleport Punishment

    [ClientRpc]
    public void TeleportPunishmentClientRpc (ulong clientId) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        if (StartOfRound.Instance.localPlayerController.playerClientId == clientId) {
            GameObject[] insideAINodes = RoundManager.Instance.insideAINodes;
            Vector3 teleportPos = insideAINodes[Random.RandomRangeInt(0, insideAINodes.Length)].transform.position;
            teleportPos = RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(teleportPos);

            StartCoroutine(TeleportPlayerCoroutine(player, teleportPos, 34));
        }

        player.beamOutBuildupParticle.Play();
        player.movementAudio.PlayOneShot(_teleporter.teleporterSpinSFX);
    }

    private IEnumerator TeleportPlayerCoroutine (PlayerControllerB player, Vector3 teleportPos, int damage = 0) {
        yield return new WaitForSeconds(3);
        player.movementAudio.Stop(true);
        TeleportPlayerServerRpc(player.playerClientId, teleportPos, damage);
    }

    [ServerRpc(RequireOwnership = false)]
    public void TeleportPlayerServerRpc (ulong clientId, Vector3 teleportPos, int damage = 0) {
        TeleportPlayerClientRpc(clientId, teleportPos, damage);
    }

    [ClientRpc]
    public void TeleportPlayerClientRpc (ulong clientId, Vector3 teleportPos, int damage = 0) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        player.movementAudio.PlayOneShot(_teleporter.teleporterBeamUpSFX);
        Plugin.TeleportPlayer(clientId, teleportPos);
        if (damage > 0) {
            if (player.health > damage + 5) player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Crushing);
            else player.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Crushing);
        }
    }

    #endregion

    #endregion

    #region Send/Receive/Sync Setting Rpcs

    [ServerRpc(RequireOwnership = false)]
    public void RequestHostCategoriesServerRpc (ulong clientId) {
        string clientUsername = StartOfRound.Instance.allPlayerScripts[clientId].playerUsername;
        Plugin.Console.LogInfo($"Received categories request from {clientUsername}, sending categories...");

        SendCategoriesClientRpc(clientId,
            Categories.Keys.Select(categoryName => new FixedString64Bytes(categoryName)).ToArray(),
            Categories.Values.Select(wordList => new FixedString512Bytes(string.Join(",", wordList))).ToArray());
    }

    [ClientRpc]
    public void SendCategoriesClientRpc (ulong clientId, FixedString64Bytes[] catNames,
        FixedString512Bytes[] catWords) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        string logMessage = $"Received the following categories from the host ({catNames.Length}):\n";
        for (int i = 0; i < catNames.Length; i++) {
            logMessage += $"{catNames[i].Value}: \"{catWords[i].Value}\"";
            if (i < catNames.Length - 1) logMessage += "\n";
        }

        Plugin.Console.LogInfo(logMessage);
    }

    #endregion

    #region Moon and Category Management Rpcs

    [ServerRpc]
    public void SetMoonInProgressServerRpc (bool roundInProgress) {
        MoonInProgress.Value = roundInProgress; // TODO: see if we still want to use a network variable?

        if (roundInProgress) {
            AddCategoryClientRpc(PickCategory(Config.SharedCategoriesPerMoon.Value));
            AddRandomCategoryClientRpc(Config.PrivateCategoriesPerMoon.Value);
            StartSpeechRecognitionClientRpc();
            DisplayCategoryHintsClientRpc();
        }
        else {
            DeactivateAllCategoriesClientRpc();
            StopSpeechRecognitionClientRpc();
        }
    }

    [ClientRpc]
    public void AddCategoryClientRpc (FixedString64Bytes[] sharedCategories) {
        foreach (FixedString64Bytes categoryName in sharedCategories) {
            ActiveCategories.Add(categoryName.ToString());
            ActiveWords.UnionWith(Categories[categoryName.ToString()]);
            Plugin.Console.LogInfo($"Added {categoryName.Value} ({Categories[categoryName.ToString()]})");
        }

        LogActiveCategories();
    }

    [ClientRpc]
    public void AddRandomCategoryClientRpc (int amount) {
        AddCategoryClientRpc(PickCategory(amount));
    }

    [ClientRpc]
    public void DeactivateAllCategoriesClientRpc () {
        ActiveCategories.Clear();
        ActiveWords.Clear();
        LogActiveCategories();
    }

    [ClientRpc]
    public void StartSpeechRecognitionClientRpc () {
        Plugin.Instance.SpeechRecognizer.StartRecognizer();
    }

    [ClientRpc]
    public void StopSpeechRecognitionClientRpc () {
        Plugin.Instance.SpeechRecognizer.StopRecognizer();
    }

    [ClientRpc]
    public void DisplayCategoryHintsClientRpc () {
        if (!DisplayCategoryHints.Value) return;
        Plugin.DisplayHUDTip("Don't talk about...", string.Join(", ", ActiveCategories));
    }

    #endregion

    #region Category Helper Functions

    private FixedString64Bytes[] PickCategory (int amount, bool allowActiveCategories = false) {
        HashSet<string> availableCategories = new();
        HashSet<string> selectedCategories = new();

        SelectableLevel currentMoon = StartOfRound.Instance.currentLevel;
        if (currentMoon.PlanetName.Equals("71 Gordion")) return Array.Empty<FixedString64Bytes>();

        availableCategories.UnionWith(GetSpawnableEnemiesAsCategories(currentMoon).Select(GetCategoryName));

        availableCategories.RemoveWhere(category => !allowActiveCategories && ActiveCategories.Contains(category));

        for (int i = 0; i < Math.Max(0, Math.Min(amount, availableCategories.Count)); i++) {
            string category = availableCategories.ElementAt(Random.RandomRangeInt(0, availableCategories.Count));
            selectedCategories.Add(category);
        }

        return selectedCategories.Select(categoryName => new FixedString64Bytes(categoryName)).ToArray();
    }

    public void AddCategory (string categoryName, string words) {
        // Validate name
        categoryName = categoryName.Trim();
        switch (categoryName.Length) {
            case > CategoryNameMaxLength:
                Plugin.Console.LogError(
                    $"Category name \"{categoryName}\" exceeds maximum length ({CategoryNameMaxLength})");
                return;
            case 0:
                Plugin.Console.LogError($"Category with words \"{words}\" has no name");
                return;
        }

        if (Categories.ContainsKey(categoryName)) {
            Plugin.Console.LogError($"A category with the name \"{categoryName}\" has already been loaded");
            return;
        }

        // Validate words (first step seems dumb, but it validates the words in the string so its ok)
        words = string.Join(",", ParseSplitString(words));
        if (words.Length > CategoryWordsMaxLength) {
            string logMessage =
                $"Words for category \"{categoryName}\" exceeds maximum length ({CategoryWordsMaxLength})";
            if (Categories.Count < Enum.GetValues(typeof(Category)).Length) {
                Plugin.Console.LogWarning(logMessage);
                words = words[..CategoryWordsMaxLength];
            }
            else {
                Plugin.Console.LogError($"{logMessage} and will not be loaded");
                return;
            }
        }
        else if (words.Length == 0) {
            string logMessage = $"No words found for category \"{categoryName}\"";
            if (Categories.Count < Enum.GetValues(typeof(Category)).Length) {
                Plugin.Console.LogWarning(logMessage);
            }
            else {
                Plugin.Console.LogError($"{logMessage} and will not be loaded");
                return;
            }
        }

        Categories.Add(categoryName, ParseSplitString(words));
        Plugin.Console.LogInfo($"Loaded category \"{categoryName}\" with words \"{words}\"");
    }

    public void AddCategory (Category category, string words) {
        AddCategory(GetCategoryName(category), words);
    }

    private static HashSet<string> ParseSplitString (string @string, char separator = ',') {
        HashSet<string> words = new();

        foreach (string word in @string.ToLower().Split(separator)) {
            string trimWord = word.Trim();
            switch (trimWord.Length) {
                case > WordMaxLength:
                    Plugin.Console.LogInfo($"\"{trimWord}\" exceeds maximum word character length ({WordMaxLength}) " +
                                           $"and will not be loaded");
                    continue;
                case 0:
                    continue;
            }

            words.Add(trimWord);
        }

        return words;
    }

    private static HashSet<Category> GetSpawnableEnemiesAsCategories (SelectableLevel level) {
        HashSet<Category> enemyCategories = new();
        enemyCategories.UnionWith(level.Enemies
            .Where(e => CategoryHelper.EnemyCategories.ContainsKey(e.enemyType.enemyName))
            .Select(e => CategoryHelper.EnemyCategories[e.enemyType.enemyName]));
        enemyCategories.UnionWith(level.OutsideEnemies
            .Where(e => CategoryHelper.EnemyCategories.ContainsKey(e.enemyType.enemyName))
            .Select(e => CategoryHelper.EnemyCategories[e.enemyType.enemyName]));
        enemyCategories.UnionWith(level.DaytimeEnemies
            .Where(e => CategoryHelper.EnemyCategories.ContainsKey(e.enemyType.enemyName))
            .Select(e => CategoryHelper.EnemyCategories[e.enemyType.enemyName]));
        return enemyCategories;
    }

    private static string GetCategoryName (Category category) {
        return CategoryHelper.SpacedCategoryNames.TryGetValue(category, out string spacedCategoryName)
            ? spacedCategoryName
            : category.ToString();
    }

    private void LogActiveCategories () {
        Plugin.Console.LogInfo(
            $"Active Categories ({ActiveCategories.Count}): {string.Join(", ", ActiveCategories)}\n" +
            $"{string.Join(", ", ActiveWords)}");
    }

    #endregion

    #region NetworkVariable OnValueChanged Functions

    private static void OnMoonInProgressChanged (bool prev, bool curr) {
        Plugin.Console.LogInfo($"MoonInProgress is now {curr}");
    }

    private static void OnDisplayCategoryHintsChanged (bool prev, bool curr) {
        Plugin.Console.LogInfo($"DisplayCategoryHints is now {curr}");
    }

    #endregion
}