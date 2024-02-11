using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using VoiceRecognitionAPI;
using Random = UnityEngine.Random;

namespace LethalVocabulary;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("me.loaforc.voicerecognitionapi")]
public class Plugin : BaseUnityPlugin {
    private const char WordSeparator = ',';
    private static ManualLogSource _logger;
    private static readonly Harmony Harmony = new(PluginInfo.PLUGIN_GUID);
    private static readonly float DefaultConfidence = 0.7f;
    private static readonly Dictionary<string, HashSet<string>> Categories = new();
    private static readonly HashSet<string> ActiveBlacklist = new();

    private static bool _checkVocabulary;
    public new static Config Config { get; internal set; }

    private void Awake() {
        // Initialize variables
        _logger = Logger;
        Config = new Config(base.Config);
        Harmony.PatchAll(typeof(Plugin));
        LoadCategoriesFromConfig();

        // Load phrases
        Voice.RegisterPhrases(GetAllWords().ToArray());
        Voice.RegisterCustomHandler((obj, recognizer) => {
            if (Config.DebugSTTWords.Value) {
                _logger.LogInfo("Heard: " + recognizer.Message + "\nConfidence: " + recognizer.Confidence);
            }
            if (!_checkVocabulary) return;

            var player = GetCurrentPlayer();
            if (player == null || player.health <= 0 || !StringIsIllegal(recognizer.Message) ||
                recognizer.Confidence < DefaultConfidence) return;

            CreateExplosion(player.transform.position);
        });

        _logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    private static void CreateExplosion(Vector3 position) {
        var posOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        Landmine.SpawnExplosion(position + posOffset, true, 1f, 0f);
    }

    // Functions for managing the active blacklist
    private static bool StringIsIllegal(string @string) {
        foreach (var word in ActiveBlacklist)
            if (@string.Contains(word))
                return true;

        return false;
    }

    // Returns the names of all selected categories
    private static void LoadCategoriesFromConfig() {
        Categories.Add("Snare Flea", GetWordsFromString(Config.SnareFleaWords.Value));
        Categories.Add("Bunker Spider", GetWordsFromString(Config.BunkerSpiderWords.Value));
        Categories.Add("Hoarding Bug", GetWordsFromString(Config.HoardingBugWords.Value));
        Categories.Add("Bracken", GetWordsFromString(Config.BrackenWords.Value));
        Categories.Add("Thumper", GetWordsFromString(Config.ThumperWords.Value));
        Categories.Add("Hygrodere", GetWordsFromString(Config.HygrodereWords.Value));
        Categories.Add("Ghost Girl", GetWordsFromString(Config.GhostGirlWords.Value));
        Categories.Add("Spore Lizard", GetWordsFromString(Config.SporeLizardWords.Value));
        Categories.Add("Nutcracker", GetWordsFromString(Config.NutCrackerWords.Value));
        Categories.Add("Jester", GetWordsFromString(Config.JesterWords.Value));
        Categories.Add("Masked", GetWordsFromString(Config.MaskedWords.Value));
        Categories.Add("Eyeless Dog", GetWordsFromString(Config.EyelessDogWords.Value));
        Categories.Add("Forest Keeper", GetWordsFromString(Config.ForestKeeperWords.Value));
        Categories.Add("Earth Leviathan", GetWordsFromString(Config.EarthLeviathanWords.Value));
        Categories.Add("Baboon Hawk", GetWordsFromString(Config.BaboonHawkWords.Value));
    }
    
    private static List<string> CreateNewBlacklist(int numberOfCategories) {
        List<string> selectedCategories = new();
        var categories = Categories.Select((t, i) => Categories.ElementAt(i).Key).ToList();

        // Clear the current blacklist and pick new categories
        ActiveBlacklist.Clear();
        for (var i = 0; i < Math.Min(numberOfCategories, Categories.Count); i++) {
            var index = Random.RandomRangeInt(0, categories.Count);
            selectedCategories.Add(categories[index]);
            categories.RemoveAt(index);
            ActiveBlacklist.UnionWith(Categories[selectedCategories.Last()]);
        }

        return selectedCategories;
    }

    // Functions for managing categories/full word list
    public static HashSet<string> GetAllWords() {
        HashSet<string> words = new();
        for (var i = 0; i < Categories.Count; i++) words.UnionWith(Categories.ElementAt(i).Value);

        return words;
    }

    private static HashSet<string> GetWordsFromString(string wordString) {
        HashSet<string> words = new();
        foreach (var word in wordString.Split(WordSeparator)) {
            // If a word is all uppercase and extended words are not enabled, do load the word
            if (StringIsAllUpper(word) && !Config.ExtendedWordsEnabled.Value) continue;
            words.Add(word.ToLower());
        }

        return words;
    }

    private static bool StringIsAllUpper(string word) {
        return word.All(c => !char.IsLetter(c) || char.IsUpper(c));
    }

    private static PlayerControllerB GetCurrentPlayer() {
        if (RoundManager.Instance != null) return RoundManager.Instance.playersManager.localPlayerController;
        return null;
    }

    // ==============================
    // Patch text-based communication
    // ==============================
    [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
    [HarmonyPrefix]
    private static void SubmitChat_performedPatch(ref HUDManager __instance) {
        var message = __instance.chatTextField.text.ToLower();
        if (_checkVocabulary && StringIsIllegal(message)) {
            CreateExplosion(__instance.localPlayer.transform.position);
            __instance.chatTextField.text = "";
        }
    }

    [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
    [HarmonyPrefix]
    private static void ParsePlayerTerminalPatch(ref Terminal __instance) {
        var command = __instance.inputFieldText.text.ToLower();
        if (_checkVocabulary && StringIsIllegal(command)) {
            CreateExplosion(GetCurrentPlayer().transform.position);
            if (command.StartsWith("transmit")) __instance.inputFieldText.text = "";
        }
    }

    // =======================
    // Patch level load states
    // =======================
    [HarmonyPatch(typeof(RoundManager), "LoadNewLevel")]
    [HarmonyPrefix]
    private static void LoadNewLevelPatch() {
        _checkVocabulary = false;
        _logger.LogInfo("Check Vocabulary = " + _checkVocabulary);
    }

    [HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
    [HarmonyPostfix]
    private static void DisconnectPatch() {
        _checkVocabulary = false;
        _logger.LogInfo("Check Vocabulary = " + _checkVocabulary);
    }

    [HarmonyPatch(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")]
    [HarmonyPostfix]
    private static void FinishGeneratingNewLevelClientRpcPatch() {
        
        HUDManager.Instance.DisplayTip("Active Categories:", 
            String.Join(",", CreateNewBlacklist(Config.CategoriesPerMoon.Value)));
        _checkVocabulary = true;
        _logger.LogInfo("Check Vocabulary = " + _checkVocabulary);
    }
}