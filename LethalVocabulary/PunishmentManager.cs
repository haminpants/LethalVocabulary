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
    private static ShipTeleporter _teleporter;
    private static StunGrenadeItem _stunGrenade;
    private static GameObject _landminePrefab;

    private readonly Dictionary<string, HashSet<string>> _categories = new();
    public readonly HashSet<string> ActiveCategories = new();
    public readonly HashSet<string> ActiveWords = new();

    public readonly NetworkVariable<bool> MoonInProgress = new();
    private Punishment _activePunishment;
    private bool _displayCategoryHints;

    public PunishmentManager () {
        Instance = this;
        MoonInProgress.OnValueChanged += OnMoonInProgressChanged;
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

    public void LoadConfig (bool unloadPreviousData = true) {
        if (unloadPreviousData) _categories.Clear();

        foreach (KeyValuePair<Category, string> entry in CategoryHelper.CategoryToConfigWords)
            AddCategory(entry.Key, entry.Value);

        // TODO: load custom words

        SetPunishmentFromName(Config.ActivePunishment.Value);

        _displayCategoryHints = Config.DisplayCategoryHints.Value;
        // TODO: make sure config loads?
    }

    public void LoadGameResources () {
        Plugin.Console.LogInfo("Loading game resources...");

        // Load object resources
        _teleporter = Resources.FindObjectsOfTypeAll<GameObject>()
            .First(o => o.gameObject.name.Contains("Inverse")).GetComponent<ShipTeleporter>();
        _stunGrenade = Resources.FindObjectsOfTypeAll<GameObject>()
            .First(o => o.GetComponent<StunGrenadeItem>()).GetComponent<StunGrenadeItem>();

        // Load hazard resources
        _landminePrefab = RoundManager.Instance.spawnableMapObjects
            .First(o => o.prefabToSpawn.GetComponentInChildren<Landmine>() != null).prefabToSpawn;

        if (_teleporter == null) Plugin.Console.LogError("Failed to locate Inverse Teleporter resource");
        if (_stunGrenade == null) Plugin.Console.LogError("Failed to locate Stun Grenade resource");
        if (_landminePrefab == null) Plugin.Console.LogError("Failed to locate Landmine prefab");
        Plugin.Console.LogInfo("Finished loading game resources!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void DisplayHUDTipServerRpc (string header, string body, bool isWarning, ulong clientId = 9999) {
        DisplayHUDTipClientRpc(header, body, isWarning, clientId);
    }

    [ClientRpc]
    public void DisplayHUDTipClientRpc (string header, string body, bool isWarning, ulong clientId) {
        if (clientId != 9999 && StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        Plugin.DisplayHUDTip(header, body, isWarning);
    }

    #region Punishments

    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId) {
        Punishment triggerPunishment = _activePunishment;
        if (triggerPunishment.Equals(Punishment.Random))
            triggerPunishment = (Punishment)Random.RandomRangeInt(1, Enum.GetValues(typeof(Punishment)).Length);

        switch (triggerPunishment) {
            case Punishment.Teleport:
                TeleportPunishmentClientRpc(clientId);
                break;
            case Punishment.Explode:
                StartCoroutine(DelayExplosionCoroutine(clientId, 1));
                break;
            case Punishment.Flash:
                CreateFlashAtPositionClientRpc(clientId);
                break;
        }
    }

    #region Teleport Punishment

    [ClientRpc]
    public void TeleportPunishmentClientRpc (ulong clientId) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        if (StartOfRound.Instance.localPlayerController.playerClientId == clientId) {
            GameObject[] insideAINodes = RoundManager.Instance.insideAINodes;
            Vector3 teleportPos = insideAINodes[Random.RandomRangeInt(0, insideAINodes.Length)].transform.position;
            teleportPos = RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(teleportPos);

            StartCoroutine(TeleportPlayerCoroutine(player, teleportPos, 48));
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
            if (player.health >= damage + 2) player.DamagePlayer(damage, causeOfDeath: CauseOfDeath.Crushing);
            else player.KillPlayer(Vector3.zero, causeOfDeath: CauseOfDeath.Crushing);
        }
    }

    #endregion
    
    #region Explosion Punishment

    private IEnumerator DelayExplosionCoroutine (ulong clientId, int delaySeconds) {
        DisplayHUDTipServerRpc("DETONATION IMMINENT", "", true, clientId);
        yield return new WaitForSeconds(1);
        Vector3 triggerPosition = StartOfRound.Instance.allPlayerScripts[clientId].transform.position;
        yield return new WaitForSeconds(delaySeconds);
        CreateExplosionAtPositionServerRpc(clientId, triggerPosition);
    }

    [ServerRpc]
    public void CreateExplosionAtPositionServerRpc (ulong clientId, Vector3 triggerPosition) {
        CreateExplosionAtPositionClientRpc(clientId, triggerPosition);
    }

    [ClientRpc]
    public void CreateExplosionAtPositionClientRpc (ulong clientId, Vector3 triggerPosition) {
        PlayerControllerB player = StartOfRound.Instance.localPlayerController;
        Landmine.SpawnExplosion(triggerPosition, true, player.playerClientId == clientId ? 4f : 0, 0);
    }

    #endregion

    #region Flash Punishment

    [ClientRpc]
    public void CreateFlashAtPositionClientRpc (ulong clientId = 9999) {
        PlayerControllerB player = StartOfRound.Instance.localPlayerController;
        if (clientId != 9999 && player.playerClientId != clientId) return;
        player.movementAudio.PlayOneShot(_stunGrenade.explodeSFX);
        StunGrenadeItem.StunExplosion(player.transform.position, true, 1, 0);
        player.DamagePlayer(30, causeOfDeath: CauseOfDeath.Blast);
    }

    #endregion

    private void SetPunishmentFromName (string punishmentName) {
        if (Enum.TryParse(punishmentName, out Punishment punishment)) {
            Plugin.Console.LogInfo($"Set active punishment to {punishment.ToString()}");
            _activePunishment = punishment;
        }
        else {
            Plugin.Console.LogWarning($"Punishment \"{punishmentName}\" does not exist, set punishment to random");
            _activePunishment = Punishment.Random;
        }
    }

    #endregion

    #region Send/Receive/Sync Setting Rpcs

    [ServerRpc(RequireOwnership = false)]
    public void RequestSettingsServerRpc (ulong clientId) {
        PlayerControllerB host = StartOfRound.Instance.localPlayerController;
        PlayerControllerB client = StartOfRound.Instance.allPlayerScripts[clientId];
        Plugin.Console.LogInfo($"Received settings request from {client.playerUsername}, attempting to sync settings");

        SyncCategoriesClientRpc(host.playerClientId, client.playerClientId,
            _categories.Keys.Select(s => new FixedString64Bytes(s)).ToArray(),
            _categories.Values.Select(s => new FixedString512Bytes(string.Join(",", s))).ToArray());
        SyncActivePunishmentClientRpc(host.playerClientId, clientId,
            new FixedString64Bytes(_activePunishment.ToString()));
        SyncDisplayCategoryHintsClientRpc(host.playerClientId, clientId, _displayCategoryHints);
    }

    [ClientRpc]
    public void SyncCategoriesClientRpc (ulong senderId, ulong clientId, FixedString64Bytes[] catNames,
        FixedString512Bytes[] catWords) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];
        _categories.Clear();

        Plugin.Console.LogInfo($"Received {catNames.Length} categories from {sender.playerUsername}!");
        for (int i = 0; i < catNames.Length; i++) AddCategory(catNames[i].Value, catWords[i].Value);
    }

    [ClientRpc]
    public void SyncActivePunishmentClientRpc (ulong senderId, ulong clientId, FixedString64Bytes punishmentName) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Plugin.Console.LogInfo($"Received \"{punishmentName}\" from {sender.playerUsername}");
        SetPunishmentFromName(punishmentName.Value);
    }

    [ClientRpc]
    public void SyncDisplayCategoryHintsClientRpc (ulong senderId, ulong clientId, bool displayCategoryHints) {
        if (StartOfRound.Instance.localPlayerController.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Plugin.Console.LogInfo($"Received DisplayCategoryHints={displayCategoryHints} from {sender.playerUsername}");
        _displayCategoryHints = displayCategoryHints;
    }

    #endregion

    #region Moon and Category Management Rpcs

    [ServerRpc]
    public void SetMoonInProgressServerRpc (bool moonInProgress) {
        MoonInProgress.Value = moonInProgress;
        
        if (moonInProgress) {
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
            ActiveWords.UnionWith(_categories[categoryName.ToString()]);
            Plugin.Console.LogInfo($"Added {categoryName.Value} ({_categories[categoryName.ToString()]})");
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
        if (!_displayCategoryHints) return;
        string hintsMessage = string.Join(", ", ActiveCategories);
        if (hintsMessage.Length == 0) return;
        Plugin.DisplayHUDTip("Don't talk about...", hintsMessage);
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

        if (_categories.ContainsKey(categoryName)) {
            Plugin.Console.LogError($"A category with the name \"{categoryName}\" has already been loaded");
            return;
        }

        // Validate words (first step seems dumb, but it validates the words in the string so its ok)
        words = string.Join(",", ParseSplitString(words));
        if (words.Length > CategoryWordsMaxLength) {
            string logMessage =
                $"Words for category \"{categoryName}\" exceeds maximum length ({CategoryWordsMaxLength})";
            if (_categories.Count < Enum.GetValues(typeof(Category)).Length) {
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
            if (_categories.Count < Enum.GetValues(typeof(Category)).Length) {
                Plugin.Console.LogWarning(logMessage);
            }
            else {
                Plugin.Console.LogError($"{logMessage} and will not be loaded");
                return;
            }
        }

        _categories.Add(categoryName, ParseSplitString(words));
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

    #endregion
}

public enum Punishment {
    Random,
    Teleport,
    Explode,
    Flash
}