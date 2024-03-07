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

    private static GameObject _landminePrefab;
    private static ShipTeleporter _teleporter;

    public readonly List<int> ActiveCategories = new();
    public readonly HashSet<string> ActiveWords = new();

    public readonly NetworkList<FixedString64Bytes> CategoryNames = new();
    public readonly NetworkList<FixedString512Bytes> CategoryWords = new();
    public readonly NetworkVariable<bool> DisplayCategoryHints = new();
    public readonly NetworkVariable<bool> MoonInProgress = new();

    public PunishmentManager () {
        Instance = this;
        CategoryWords.OnListChanged += OnCategoriesChanged;
        MoonInProgress.OnValueChanged += OnMoonInProgressChanged;
        DisplayCategoryHints.OnValueChanged += OnDisplayCategoryHintsChanged;
    }

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

    #region Teleport Punishment Rpcs

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

    #region Moon and Category Management Rpcs

    [ServerRpc]
    public void SetMoonInProgressServerRpc (bool roundInProgress) {
        MoonInProgress.Value = roundInProgress;

        if (roundInProgress) {
            AddSharedCategoryClientRpc(PickCategory(Config.SharedCategoriesPerMoon.Value));
            AddPrivateCategoryClientRpc(Config.PrivateCategoriesPerMoon.Value);
            StartSpeechRecognitionClientRpc();
            DisplayCategoryHintsClientRpc();
        }
        else {
            DeactivateAllCategoriesClientRpc();
            StopSpeechRecognitionClientRpc();
        }
    }

    [ClientRpc]
    public void AddSharedCategoryClientRpc (int[] categoryIndexes) {
        ActiveCategories.AddRange(categoryIndexes);

        foreach (string categoryWords in categoryIndexes.Select(index => CategoryWords[index].Value))
            ActiveWords.UnionWith(ParseSplitString(categoryWords));

        LogActiveCategories();
    }

    [ClientRpc]
    public void AddPrivateCategoryClientRpc (int amount) {
        int[] categoryIndexes = PickCategory(amount);
        ActiveCategories.AddRange(categoryIndexes);

        foreach (string categoryWords in categoryIndexes.Select(index => CategoryWords[index].Value))
            ActiveWords.UnionWith(ParseSplitString(categoryWords));

        LogActiveCategories();
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
        Plugin.DisplayHUDTip("Don't talk about...", string.Join(", ", ActiveCategories.Select(i => CategoryNames[i])));
    }

    #endregion

    #region Helper Functions

    private int[] PickCategory (int amount, bool allowActiveCategories = false) {
        List<int> availableCategoryIndexes = new();
        HashSet<int> selectedCategoryIndexes = new();

        SelectableLevel currentMoon = StartOfRound.Instance.currentLevel;
        if (currentMoon.PlanetName.Equals("71 Gordion")) return Array.Empty<int>();

        availableCategoryIndexes.AddRange(GetSpawnableEnemiesAsCategories(currentMoon)
            .Select(category => (int)category));

        availableCategoryIndexes.RemoveAll(index =>
            !allowActiveCategories && (ActiveCategories.Contains(index) || ActiveCategories.Contains(index * -1)));

        for (int i = 0; i < Math.Max(0, Math.Min(amount, availableCategoryIndexes.Count)); i++) {
            int index = availableCategoryIndexes[Random.RandomRangeInt(0, availableCategoryIndexes.Count)];
            selectedCategoryIndexes.Add(index);
        }

        return selectedCategoryIndexes.ToArray();
    }

    public void AddCategory (string categoryName, string words) {
        // Validate name
        categoryName = categoryName.Trim();
        switch (categoryName.Length) {
            case > CategoryNameMaxLength:
                Plugin.Console.LogError($"Category name \"{categoryName}\" exceeds maximum length ({CategoryNameMaxLength})");
                return;
            case 0:
                Plugin.Console.LogError($"Category with words \"{words}\" has no name");
                return;
        }

        if (CategoryNames.Contains(categoryName)) {
            Plugin.Console.LogError($"A category with the name \"{categoryName}\" has already been loaded");
            return;
        }

        // Validate words
        words = string.Join(",", ParseSplitString(words));
        if (words.Length > CategoryWordsMaxLength) {
            string logMessage = $"Words for category \"{categoryName}\" exceeds maximum length ({CategoryWordsMaxLength})";
            if (CategoryNames.Count < Enum.GetValues(typeof(Category)).Length) {
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
            if (CategoryNames.Count < Enum.GetValues(typeof(Category)).Length) {
                Plugin.Console.LogWarning(logMessage);
            }
            else {
                Plugin.Console.LogError($"{logMessage} and will not be loaded");
                return;
            }
        }

        CategoryNames.Add(categoryName);
        CategoryWords.Add(words);
    }

    public void AddCategory (Category category, string words) {
        string categoryName = CategoryHelper.SpacedCategoryNames.TryGetValue(category, out string spacedCategoryName)
            ? spacedCategoryName
            : category.ToString();
        AddCategory(categoryName, words);
    }

    private static IEnumerable<string> ParseSplitString (string @string, char separator = ',') {
        HashSet<string> words = new();

        foreach (string word in @string.ToLower().Split(separator)) {
            string trimWord = word.Trim();
            if (trimWord.Length is 0 or > WordMaxLength) {
                Plugin.Console.LogWarning($"\"{trimWord}\" exceeds maximum word character length ({WordMaxLength}) " +
                                          $"and will not be loaded");
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

    public void LogActiveCategories () {
        Plugin.Console.LogInfo($"Active Categories ({ActiveCategories.Count}): " +
                               $"{string.Join(", ", ActiveCategories.Select(i => CategoryNames[i]))}");
    }

    #endregion

    #region NetworkVariable OnValueChanged Functions

    private static void OnCategoriesChanged (NetworkListEvent<FixedString512Bytes> @event) {
        int index = @event.Index;
        string words = @event.Value.ToString();
        Plugin.Console.LogInfo($"Loaded \"{Instance.CategoryNames[index]}\" with words \"{words}\" (index={index})");
    }

    private static void OnMoonInProgressChanged (bool prev, bool curr) {
        Plugin.Console.LogInfo($"MoonInProgress is now {curr}");
    }

    private static void OnDisplayCategoryHintsChanged (bool prev, bool curr) {
        Plugin.Console.LogInfo($"DisplayCategoryHints is now {curr}");
    }

    #endregion
}