using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalVocabulary;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    private const char WordSeparator = ',';
    public static ManualLogSource logger;
    private static readonly Harmony Harmony = new(PluginInfo.PLUGIN_GUID);

    private static SpeechRecognizer _speechRecognizer;
    private static readonly Dictionary<string, HashSet<string>> Categories = new();
    private static readonly HashSet<string> ActiveBlacklist = new();
    private static bool _checkVocabulary;

    public new static Config Config { get; internal set; }

    private void Awake () {
        // Initialize variables
        logger = Logger;
        Harmony.PatchAll(typeof(Plugin));
        Config = new Config(base.Config);
        LoadCategoriesFromConfig();
        _speechRecognizer = new SpeechRecognizer();

        logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    public static void ProcessSpeech (object sender, SpeechRecognizedEventArgs e) {
        var speech = e.Result.Text;
        var confidence = e.Result.Confidence;

        logger.LogInfo("Heard \"" + speech + "\" with " + confidence + " confidence.");

        if (!StringIsIllegal(speech)) return;
        var player = RoundManager.Instance != null
            ? RoundManager.Instance.playersManager.localPlayerController
            : null;
        if (player != null) CreateExplosion(player.transform.position);
    }

    // ================
    // Helper functions
    // ================
    private static bool StringIsIllegal (string @string) {
        foreach (var word in ActiveBlacklist)
            if (@string.Contains(word))
                return true;
        return false;
    }

    private static void CreateExplosion (Vector3 position) {
        var posOffset = new Vector3(Random.Range(-0.5f, 0.5f), 0, Random.Range(-0.5f, 0.5f));
        Landmine.SpawnExplosion(position + posOffset, true, 1f, 0f);
    }

    private static void DisplayHUDTip (string title, string description, bool isWarning) {
        if (HUDManager.Instance == null) {
            logger.LogError("Failed to display tip: " + title + " - " + description);
            return;
        }

        HUDManager.Instance.DisplayTip(title, description, isWarning);
        logger.LogInfo("Displayed Tip: " + title + " - " + description);
    }

    public static string[] GetAllWords () {
        HashSet<string> words = new();
        for (var i = 0; i < Categories.Count; i++) words.UnionWith(Categories.ElementAt(i).Value);

        return words.ToArray();
    }

    // Functions for managing the active blacklist
    private static List<string> PickRandomCategories (int numberOfCategories) {
        var categories = Categories.Select((t, i) => Categories.ElementAt(i).Key).ToList();
        List<string> selectedCategories = new();

        for (var i = 0; i < Math.Min(numberOfCategories, categories.Count); i++) {
            var categoryIndex = Random.RandomRangeInt(0, categories.Count);
            selectedCategories.Add(categories[categoryIndex]);
            categories.RemoveAt(categoryIndex);
        }

        return selectedCategories;
    }

    // Functions for managing categories/full word list
    private static void LoadCategoriesFromConfig () {
        Categories.Add("Snare Flea", GetWordsFromString(Config.SnareFleaWords.Value));
        Categories.Add("Bunker Spider", GetWordsFromString(Config.BunkerSpiderWords.Value));
        Categories.Add("Hoarding Bug", GetWordsFromString(Config.HoardingBugWords.Value));
        Categories.Add("Bracken", GetWordsFromString(Config.BrackenWords.Value));
        Categories.Add("Thumper", GetWordsFromString(Config.ThumperWords.Value));
        Categories.Add("Hygrodere", GetWordsFromString(Config.HygrodereWords.Value));
        Categories.Add("Ghost Girl", GetWordsFromString(Config.GhostGirlWords.Value));
        Categories.Add("Spore Lizard", GetWordsFromString(Config.SporeLizardWords.Value));
        Categories.Add("Nutcracker", GetWordsFromString(Config.NutCrackerWords.Value));
        Categories.Add("Coil Head", GetWordsFromString(Config.CoilHeadWords.Value));
        Categories.Add("Jester", GetWordsFromString(Config.JesterWords.Value));
        Categories.Add("Masked", GetWordsFromString(Config.MaskedWords.Value));
        Categories.Add("Eyeless Dog", GetWordsFromString(Config.EyelessDogWords.Value));
        Categories.Add("Forest Keeper", GetWordsFromString(Config.ForestKeeperWords.Value));
        Categories.Add("Earth Leviathan", GetWordsFromString(Config.EarthLeviathanWords.Value));
        Categories.Add("Baboon Hawk", GetWordsFromString(Config.BaboonHawkWords.Value));
    }

    private static HashSet<string> GetWordsFromString (string wordString) {
        HashSet<string> words = new();
        foreach (var word in wordString.Split(WordSeparator)) {
            // If a word is all uppercase and extended words are not enabled, add the word to the set
            if (StringIsAllUpper(word) && !Config.ExtendedWordsEnabled.Value) continue;
            words.Add(word.ToLower());
        }

        return words;
    }

    private static bool StringIsAllUpper (string word) {
        return word.All(c => !char.IsLetter(c) || char.IsUpper(c));
    }

    // ==============================
    // Patch text-based communication
    // ==============================
    [HarmonyPatch(typeof(HUDManager), "SubmitChat_performed")]
    [HarmonyPrefix]
    private static void SubmitChat_performedPatch (ref HUDManager __instance) {
        var message = __instance.chatTextField.text.ToLower();
        if (_checkVocabulary && StringIsIllegal(message)) {
            CreateExplosion(__instance.localPlayer.transform.position);
            __instance.chatTextField.text = "";
        }
    }

    [HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
    [HarmonyPrefix]
    private static void ParsePlayerTerminalPatch (ref Terminal __instance) {
        var command = __instance.inputFieldText.text.ToLower();
        if (_checkVocabulary && StringIsIllegal(command)) {
            CreateExplosion(__instance.roundManager.playersManager.localPlayerController.transform.position);
            if (command.StartsWith("transmit")) __instance.inputFieldText.text = "";
        }
    }

    // =======================
    // Patch level load states
    // =======================
    [HarmonyPatch(typeof(RoundManager), "FinishGeneratingNewLevelClientRpc")]
    [HarmonyPostfix]
    private static void FinishGeneratingNewLevelClientRpcPatch () {
        if (ActiveBlacklist.Count <= 0) {
            var categories = PickRandomCategories(Config.CategoriesPerMoon.Value);
            DisplayHUDTip("Don't talk about...", string.Join(", ", categories), false);
            foreach (var category in categories) ActiveBlacklist.UnionWith(Categories[category]);
        }

        _checkVocabulary = true;
    }

    [HarmonyPatch(typeof(StartOfRound), "EndOfGameClientRpc")]
    [HarmonyPostfix]
    private static void EndOfGameClientRpcPatch () {
        ActiveBlacklist.Clear();
        _checkVocabulary = false;
    }

    [HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
    [HarmonyPostfix]
    private static void DisconnectPatch () {
        EndOfGameClientRpcPatch();
    }
}