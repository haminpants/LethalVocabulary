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
    public static PunishmentManager Instance;
    public const int CategoryNameMaxLength = 64;
    public const int CategoryWordsMaxLength = 512;
    public const int WordMaxLength = 128;


    // Quick Access Resources 
    private static PlayerControllerB _localPlayer;
    private static ShipTeleporter _teleporter;
    private static StunGrenadeItem _stunGrenade;
    private static GameObject _snareFleaPrefab;

    // Category Variables
    public readonly Dictionary<string, HashSet<string>> Categories = new();
    private readonly HashSet<string> _sharedCategories = new();
    private readonly HashSet<string> _privateCategories = new();
    private readonly HashSet<string> _categoryWords = new();

    // Game Settings
    public readonly NetworkVariable<bool> MoonInProgress = new();
    public Punishment activePunishment;
    public int sharedCategoriesPerMoon;
    public int privateCategoriesPerMoon;
    public bool sharedCategoryHints;
    public bool privateCategoryHints;
    private readonly HashSet<string> _forcedCategories = new();

    // Punishment Variables
    private Coroutine _apologyTimer;
    public int apologyIndex = -1;

    public PunishmentManager () {
        Instance = this;
        MoonInProgress.OnValueChanged += OnMoonInProgressChanged;
    }

    public bool StringIsLegal (string message, double confidence = 1) {
        message = message.Trim().ToLower();

        if (!MoonInProgress.Value || _localPlayer.isPlayerDead || confidence < Config.ConfidenceThreshold.Value)
            return true;

        if (!_categoryWords.Any(word => message.Contains(word))) return true;
        PunishPlayerServerRpc(_localPlayer.playerClientId);
        return false;

    }

    public void LoadConfig (bool unloadPreviousData = true) {
        if (unloadPreviousData) Categories.Clear();

        foreach (KeyValuePair<Category, string> entry in CategoryHelper.CategoryToConfigWords)
            AddCategory(entry.Key, entry.Value);

        // TODO: load custom words

        SetPunishmentFromName(Config.ActivePunishment.Value);

        sharedCategoriesPerMoon = Config.SharedCategoriesPerMoon.Value;
        privateCategoriesPerMoon = Config.PrivateCategoriesPerMoon.Value;
        sharedCategoryHints = Config.SharedCategoryHints.Value;
        privateCategoryHints = Config.PrivateCategoryHints.Value;
        if (Config.PunishCurseWords.Value) _forcedCategories.Add(GetCategoryName(Category.CurseWords));
    }

    public void LoadGameResources () {
        Plugin.Console.LogInfo("Loading game resources...");
        _localPlayer = StartOfRound.Instance.localPlayerController;

        // Load object resources
        _teleporter = Resources.FindObjectsOfTypeAll<GameObject>()
            .First(o => o.gameObject.name.Contains("Inverse")).GetComponent<ShipTeleporter>();
        _stunGrenade = Resources.FindObjectsOfTypeAll<GameObject>()
            .First(o => o.GetComponent<StunGrenadeItem>()).GetComponent<StunGrenadeItem>();

        // Load enemy prefabs
        HashSet<GameObject> enemyPrefabs = new();
        foreach (SelectableLevel level in StartOfRound.Instance.levels) {
            enemyPrefabs.UnionWith(level.Enemies.Select(o => o.enemyType.enemyPrefab));
            enemyPrefabs.UnionWith(level.OutsideEnemies.Select(o => o.enemyType.enemyPrefab));
        }

        _snareFleaPrefab = enemyPrefabs.First(o => o.GetComponent<CentipedeAI>() != null);

        if (_teleporter == null) Plugin.Console.LogError("Failed to locate Inverse Teleporter resource");
        if (_stunGrenade == null) Plugin.Console.LogError("Failed to locate Stun Grenade resource");
        if (_snareFleaPrefab == null) Plugin.Console.LogError("Failed to locate Snare Flea prefab");
        Plugin.Console.LogInfo("Finished loading game resources!");
    }

    [ServerRpc(RequireOwnership = false)]
    public void DisplayHUDTipServerRpc (string header, string body, bool isWarning, ulong clientId = 9999) {
        DisplayHUDTipClientRpc(header, body, isWarning, clientId);
    }

    [ClientRpc]
    public void DisplayHUDTipClientRpc (string header, string body, bool isWarning, ulong clientId) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        Plugin.DisplayHUDTip(header, body, isWarning);
    }

    #region NetworkVariable OnValueChanged Functions

    private static void OnMoonInProgressChanged (bool prev, bool curr) {
        Plugin.Console.LogInfo($"MoonInProgress is now {curr}");
    }

    #endregion

    #region Punishments

    [ServerRpc(RequireOwnership = false)]
    public void PunishPlayerServerRpc (ulong clientId, bool forceRandom = false) {
        Punishment triggerPunishment = activePunishment;
        if (forceRandom || triggerPunishment.Equals(Punishment.Random))
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
            case Punishment.Apologize:
                ForceApologyClientRpc(clientId);
                break;
            case Punishment.Suffocate:
                SuffocatePlayerServerRpc(clientId);
                break;
        }
    }

    #region Teleport Punishment

    [ClientRpc]
    public void TeleportPunishmentClientRpc (ulong clientId) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        if (_localPlayer.playerClientId == clientId) {
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

    [ServerRpc(RequireOwnership = false)]
    public void CreateExplosionAtPositionServerRpc (ulong clientId, Vector3 triggerPosition) {
        CreateExplosionAtPositionClientRpc(clientId, triggerPosition);
    }

    [ClientRpc]
    public void CreateExplosionAtPositionClientRpc (ulong clientId, Vector3 triggerPosition) {
        Landmine.SpawnExplosion(triggerPosition, true, _localPlayer.playerClientId == clientId ? 4f : 0, 0);
    }

    #endregion

    #region Flash Punishment

    [ClientRpc]
    public void CreateFlashAtPositionClientRpc (ulong clientId) {
        if (_localPlayer.playerClientId != clientId) return;
        _localPlayer.movementAudio.PlayOneShot(_stunGrenade.explodeSFX);
        StunGrenadeItem.StunExplosion(_localPlayer.transform.position, true, 1, 0);
        _localPlayer.DamagePlayer(30, causeOfDeath: CauseOfDeath.Blast);
    }

    #endregion

    #region Apology Punishment

    // TODO: put these in a file and read them later, sync them with the host
    public static readonly string[] Apologies = {
        "I didn't mean to say that.",
        "I was just kidding.",
        "I lied, ignore me.",
        "I will not say that again.",
        "I will think before speaking.",
        "I shouldn't have said that."
    };

    [ClientRpc]
    public void ForceApologyClientRpc (ulong clientId) {
        if (_localPlayer.playerClientId != clientId) return;
        if (_apologyTimer != null) {
            Plugin.Console.LogWarning("Apology punishment already active, executing alternative punishment");
            PunishPlayerServerRpc(clientId, true);
            return;
        }

        apologyIndex = Random.RandomRangeInt(0, Apologies.Length);
        _apologyTimer = StartCoroutine(StartApologyTimer(10));
    }

    private IEnumerator StartApologyTimer (int timerSeconds) {
        if (apologyIndex < 0) {
            Plugin.Console.LogWarning("An apology was not picked, defaulting to apology 0");
            apologyIndex = 0;
        }

        Plugin.Console.LogInfo($"APOLOGY TIMER STARTED: {timerSeconds} seconds to type {Apologies[apologyIndex]}");
        for (int i = timerSeconds; i > 0; i--) {
            Plugin.DisplayHUDTip($"APOLOGIZE IN CHAT ({i})", $"Send \"{Apologies[apologyIndex]}\"", true);
            yield return new WaitForSeconds(1);
        }

        CreateExplosionAtPositionServerRpc(_localPlayer.playerClientId, _localPlayer.transform.position);
        StopApologyTimer(false);
    }

    public void StopApologyTimer (bool showPassMessage = true) {
        StopCoroutine(_apologyTimer);
        _apologyTimer = null;
        apologyIndex = -1;

        if (showPassMessage) Plugin.DisplayHUDTip("Thank you for apologizing.", "");
        Plugin.Console.LogInfo("Apology timer has stopped!");
    }

    #endregion

    #region Suffocate Punishment

    [ServerRpc]
    public void SuffocatePlayerServerRpc (ulong clientId) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        if (!player.isInsideFactory) {
            Plugin.Console.LogWarning("Player not in facility, executing alternative punishment");
            // TODO: execute alternative punishment, pink smoke from lizard and muffle?
            PunishPlayerServerRpc(clientId, true);
            return;
        }

        GameObject snareFleaObject =
            Instantiate(_snareFleaPrefab, player.gameObject.transform.position, Quaternion.identity);
        CentipedeAI snareFlea = snareFleaObject.GetComponent<CentipedeAI>();
        snareFlea.GetComponent<EnemyAI>().enemyHP = 1;
        snareFlea.GetComponent<NetworkObject>().Spawn();
        StartCoroutine(ClingToPlayer(clientId, snareFlea));
        StartCoroutine(KillSnareFleaOnStopClinging(snareFlea));
    }

    private static IEnumerator ClingToPlayer (ulong clientId, CentipedeAI snareFlea) {
        while (snareFlea.agent == null) yield return null;
        snareFlea.ClingToPlayerServerRpc(clientId);
        yield return new WaitForSeconds(6.1f);
        if (snareFlea.clingingToPlayer != null) snareFlea.StopClingingServerRpc(false);
    }

    private static IEnumerator KillSnareFleaOnStopClinging (CentipedeAI snareFlea) {
        yield return new WaitForSeconds(1);
        while (snareFlea.clingingToPlayer != null) {
            if (snareFlea.isEnemyDead) yield break;
            yield return null;
        }

        snareFlea.KillEnemyOnOwnerClient();
    }

    #endregion

    private void SetPunishmentFromName (string punishmentName) {
        if (Enum.TryParse(punishmentName, out Punishment punishment)) {
            Plugin.Console.LogInfo($"Set active punishment to {punishment.ToString()}");
            activePunishment = punishment;
        }
        else {
            Plugin.Console.LogWarning($"Punishment \"{punishmentName}\" does not exist, set punishment to random");
            activePunishment = Punishment.Random;
        }
    }

    #endregion

    #region Send/Receive/Sync Setting Rpcs

    [ServerRpc(RequireOwnership = false)]
    public void RequestSettingsServerRpc (ulong clientId) {
        string clientName = StartOfRound.Instance.allPlayerScripts[clientId].playerUsername;
        Plugin.Console.LogInfo($"Received settings request from {clientName}, attempting to sync settings");

        SyncCategoriesClientRpc(_localPlayer.playerClientId,
            Categories.Keys.Select(s => new FixedString64Bytes(s)).ToArray(),
            Categories.Values.Select(s => new FixedString512Bytes(string.Join(",", s))).ToArray(),
            clientId);

        SyncActivePunishmentClientRpc(_localPlayer.playerClientId,
            new FixedString64Bytes(activePunishment.ToString()), clientId);

        SyncCategoriesPerMoonClientRpc(_localPlayer.playerClientId,
            sharedCategoriesPerMoon, privateCategoriesPerMoon, clientId);

        SyncDisplayCategoryHintsClientRpc(_localPlayer.playerClientId,
            sharedCategoryHints, privateCategoryHints, clientId);

        SyncForcedCategoriesClientRpc(_localPlayer.playerClientId,
            _forcedCategories.Select(c => new FixedString64Bytes(c)).ToArray(), clientId);
    }

    #region Category Sync Functions

    [ClientRpc]
    public void SyncCategoriesClientRpc (ulong senderId, FixedString64Bytes[] categoryNames,
        FixedString512Bytes[] categoryWords, ulong clientId = 9999) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        if (_localPlayer.IsHost) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Categories.Clear();
        Plugin.Console.LogInfo($"Received {categoryNames.Length} categories from {sender.playerUsername}!");
        for (int i = 0; i < categoryNames.Length; i++) AddCategory(categoryNames[i].Value, categoryWords[i].Value);
    }

    #endregion

    #region Active Punishment Sync Functions

    [ServerRpc]
    public void SetActivePunishmentServerRpc (FixedString64Bytes punishmentName) {
        SyncActivePunishmentClientRpc(_localPlayer.playerClientId, punishmentName);
    }

    [ClientRpc]
    public void SyncActivePunishmentClientRpc (ulong senderId, FixedString64Bytes punishmentName,
        ulong clientId = 9999) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Plugin.Console.LogInfo($"Received \"{punishmentName}\" from {sender.playerUsername}");
        SetPunishmentFromName(punishmentName.Value);
    }

    #endregion

    #region Categories Per Moon Sync Functions

    [ServerRpc]
    public void SetCategoriesPerMoonServerRpc (int shared = -1, int @private = -1) {
        shared = shared >= 0 ? shared : sharedCategoriesPerMoon;
        @private = @private >= 0 ? @private : privateCategoriesPerMoon;
        SyncCategoriesPerMoonClientRpc(_localPlayer.playerClientId, shared, @private);
    }

    [ClientRpc]
    public void SyncCategoriesPerMoonClientRpc (ulong senderId, int shared, int @private, ulong clientId = 9999) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Plugin.Console.LogInfo($"Received SharedCategoriesPerMoon={shared} and PrivateCategoriesPerMoon={@private} " +
                               $"from {sender.playerUsername}");
        sharedCategoriesPerMoon = shared;
        privateCategoriesPerMoon = @private;
    }

    #endregion

    #region Display Categories Hints Sync Functions

    [ServerRpc]
    public void SetCategoryHintsServerRpc (bool sharedCategoryHints, bool privateCategoryHints) {
        SyncDisplayCategoryHintsClientRpc(_localPlayer.playerClientId, sharedCategoryHints, privateCategoryHints);
    }

    [ClientRpc]
    public void SyncDisplayCategoryHintsClientRpc (ulong senderId, bool sharedCategoryHints, bool privateCategoryHints,
        ulong clientId = 9999) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        Plugin.Console.LogInfo($"Received SharedCategoryHints={sharedCategoryHints} and " +
                               $"PrivateCategoryHints={privateCategoryHints} from {sender.playerUsername}");
        this.sharedCategoryHints = sharedCategoryHints;
        this.privateCategoryHints = privateCategoryHints;
    }

    #endregion

    #region Forced Categories Sync Functions

    [ServerRpc]
    public void SetForcedCategoryStatusServerRpc (Category category, bool setForced) {
        string categoryName = GetCategoryName(category);
        if (setForced) {
            if (_forcedCategories.Add(categoryName)) {
                Plugin.Console.LogInfo($"Added {categoryName} to Forced Categories");
            }
            else {
                Plugin.Console.LogWarning($"{categoryName} is already in the list of Forced Categories!");
                return;
            }
        }
        else {
            if (_forcedCategories.Remove(categoryName)) {
                Plugin.Console.LogInfo($"Removed {categoryName} from Forced Categories");
            }
            else {
                Plugin.Console.LogWarning($"{categoryName} is not in the list of Forced Categories ");
                return;
            }
        }

        SyncForcedCategoriesClientRpc(_localPlayer.playerClientId,
            _forcedCategories.Select(c => new FixedString64Bytes(c)).ToArray());
    }

    [ClientRpc]
    public void SyncForcedCategoriesClientRpc (ulong senderId, FixedString64Bytes[] forcedCategories,
        ulong clientId = 9999) {
        if (clientId != 9999 && _localPlayer.playerClientId != clientId) return;
        if (_localPlayer.IsHost) return;
        PlayerControllerB sender = StartOfRound.Instance.allPlayerScripts[senderId];

        string[] categories = forcedCategories.Select(c => c.Value).ToArray();
        Plugin.Console.LogInfo($"Received ForcedCategories={string.Join(",", categories)} " +
                               $"from {sender.playerUsername}");
        _forcedCategories.Clear();
        _forcedCategories.UnionWith(categories);
    }

    #endregion

    #endregion

    #region Moon and Category Management Rpcs

    [ServerRpc]
    public void SetMoonInProgressServerRpc (bool moonInProgress) {
        MoonInProgress.Value = moonInProgress;

        if (moonInProgress) {
            AddSharedCategoriesClientRpc(PickCategory(sharedCategoriesPerMoon));
            AddPrivateCategoriesClientRpc(privateCategoriesPerMoon);
            UpdateCategoryWordsClientRpc();

            StartSpeechRecognitionClientRpc();
            DisplayCategoryHintsClientRpc();
        }
        else {
            DeactivateAllCategoriesClientRpc();
            StopSpeechRecognitionClientRpc();
        }
    }

    [ClientRpc]
    public void AddSharedCategoriesClientRpc (FixedString64Bytes[] sharedCategories) {
        foreach (FixedString64Bytes category in sharedCategories) {
            _sharedCategories.Add(category.Value);
            Plugin.Console.LogInfo($"Added Shared Category: {category.Value} - " +
                                   $"\"{string.Join(", ", Categories[category.Value])}\"");
        }
    }

    [ClientRpc]
    public void AddPrivateCategoriesClientRpc (int amount) {
        foreach (FixedString64Bytes category in PickCategory(amount)) {
            _privateCategories.Add(category.Value);
            Plugin.Console.LogInfo($"Added Private Category: {category.Value} - " +
                                   $"\"{string.Join(", ", Categories[category.Value])}\"");
        }
    }

    [ClientRpc]
    public void UpdateCategoryWordsClientRpc () {
        _categoryWords.Clear();
        foreach (string category in _forcedCategories) _categoryWords.UnionWith(Categories[category]);
        foreach (string category in _sharedCategories) _categoryWords.UnionWith(Categories[category]);
        foreach (string category in _privateCategories) _categoryWords.UnionWith(Categories[category]);
        LogCategories();
    }

    [ClientRpc]
    public void DeactivateAllCategoriesClientRpc () {
        _sharedCategories.Clear();
        _privateCategories.Clear();
        _categoryWords.Clear();
        LogCategories();
    }

    [ClientRpc]
    public void StartSpeechRecognitionClientRpc () {
        Plugin.Instance.SpeechRecognizer.StartRecognizer(_categoryWords);
    }

    [ClientRpc]
    public void StopSpeechRecognitionClientRpc () {
        Plugin.Instance.SpeechRecognizer.StopRecognizer();
    }

    [ClientRpc]
    public void DisplayCategoryHintsClientRpc () {
        HashSet<string> categories = new();
        categories.UnionWith(_forcedCategories); // TODO: this might need to change later
        if (sharedCategoryHints) categories.UnionWith(_sharedCategories);
        if (privateCategoryHints) categories.UnionWith(_privateCategories);
        string hintsMessage = string.Join(", ", categories);

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

        availableCategories.RemoveWhere(category => !allowActiveCategories && _forcedCategories.Contains(category));
        availableCategories.RemoveWhere(category => !allowActiveCategories && _sharedCategories.Contains(category));
        availableCategories.RemoveWhere(category => !allowActiveCategories && _privateCategories.Contains(category));

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

    private void LogCategories () {
        Plugin.Console.LogInfo(
            $"Forced Categories ({_forcedCategories.Count}): {string.Join(", ", _forcedCategories)}");
        Plugin.Console.LogInfo(
            $"Shared Categories ({_sharedCategories.Count}): {string.Join(", ", _sharedCategories)}");
        Plugin.Console.LogInfo(
            $"Private Categories ({_privateCategories.Count}): {string.Join(", ", _privateCategories)}");
        Plugin.Console.LogInfo($"Loaded Words ({_categoryWords.Count}): {string.Join(", ", _categoryWords)}");
    }

    public bool IsPunishCurseWords () {
        return _forcedCategories.Contains(GetCategoryName(Category.CurseWords));
    }

    #endregion
}

public enum Punishment {
    Random,
    Teleport,
    Explode,
    Flash,
    Apologize,
    Suffocate
}