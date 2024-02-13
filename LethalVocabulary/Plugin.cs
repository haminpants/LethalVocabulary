using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LethalVocabulary;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public static Plugin Instance;
    private static Harmony _harmony;
    public static ManualLogSource logger;
    public GameObject penaltyManagerPrefab;

    public bool roundInProgress;
    public readonly HashSet<string> ClientBannedCategories = new();
    public readonly HashSet<string> ClientBannedWords = new();
    public Dictionary<string, HashSet<string>> ClientCategories = new();
    private SpeechRecognizer _speechRecognizer;
    
    public new static Config Config { get; internal set; }

    private void Awake () {
        // Patch netcode
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types) {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods) {
                var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0) method.Invoke(null, null);
            }
        }

        // Initialize variables
        Instance = this;
        _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        logger = Logger;
        Config = new Config(base.Config);
        _speechRecognizer = new SpeechRecognizer();

        // Load assets
        var assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "lethalvocabularybundle");
        var bundle = AssetBundle.LoadFromFile(assetDir);
        penaltyManagerPrefab = bundle.LoadAsset<GameObject>("Assets/LethalVocabulary/PenaltyManager.prefab");
        penaltyManagerPrefab.AddComponent<PenaltyManager>();

        // Load config
        ClientCategories = GetCategoriesFromConfig(Config.EnableExtendedWords.Value);

        // Add event listener to the speech recognizer
        _speechRecognizer.AddSpeechRecognizedHandler((_, e) => {
            var speech = e.Result.Text;
            var confidence = e.Result.Confidence;
            Logger.LogError("Heard \"" + speech + "\" with " + confidence * 100 + "% confidence");

            if (!roundInProgress || confidence < 0.85f || RoundManager.Instance == null) return;
            var player = RoundManager.Instance.playersManager.localPlayerController;
            if (player == null || player.isPlayerDead || !StringHasWords(speech, ClientBannedWords)) return;
            PenaltyManager.Instance.PunishPlayerServerRpc(player.NetworkManager.LocalClientId);
        });

        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    public void StartRound () {
        if (roundInProgress) return;
        ClientBannedCategories.UnionWith(PickRandomCategories(Math.Max(1, Config.CategoriesPerMoon.Value)));
        foreach (var category in ClientBannedCategories) ClientBannedWords.UnionWith(ClientCategories[category]);
        DisplayHUDTip("Don't talk about...", string.Join(", ", ClientBannedCategories), false);
        Logger.LogError(string.Join(", ", ClientBannedWords));
        roundInProgress = true;
    }

    public void EndRound () {
        roundInProgress = false;
        ClientBannedCategories.Clear();
        ClientBannedWords.Clear();
    }

    // ================
    // Helper functions
    // ================
    public string[] PickRandomCategories (int amount) {
        var categories = ClientCategories.Keys.ToList();
        List<string> pickedCategories = new();

        for (var i = 0; i < Math.Min(amount, categories.Count); i++) {
            var pickedIndex = Random.RandomRangeInt(0, categories.Count);
            pickedCategories.Add(categories[pickedIndex]);
            categories.RemoveAt(pickedIndex);
        }

        return pickedCategories.ToArray();
    }

    public static string[] GetAllWordsFromConfig (bool getExtendedWords) {
        HashSet<string> words = new();
        foreach (var entry in GetCategoriesFromConfig(getExtendedWords)) words.UnionWith(entry.Value);

        return words.ToArray();
    }

    public static Dictionary<string, HashSet<string>> GetCategoriesFromConfig (bool getExtendedWords) {
        Dictionary<string, HashSet<string>> categories = new();
        categories.Add("Snare Flea", ProcessCategoryWords(Config.SnareFleaWords.Value, getExtendedWords));
        categories.Add("Bunker Spider", ProcessCategoryWords(Config.BunkerSpiderWords.Value, getExtendedWords));
        categories.Add("Hoarding Bug", ProcessCategoryWords(Config.HoardingBugWords.Value, getExtendedWords));
        categories.Add("Bracken", ProcessCategoryWords(Config.BrackenWords.Value, getExtendedWords));
        categories.Add("Thumper", ProcessCategoryWords(Config.ThumperWords.Value, getExtendedWords));
        categories.Add("Hygrodere (Slime)", ProcessCategoryWords(Config.HygrodereWords.Value, getExtendedWords));
        categories.Add("Ghost Girl", ProcessCategoryWords(Config.GhostGirlWords.Value, getExtendedWords));
        categories.Add("Spore Lizard", ProcessCategoryWords(Config.SporeLizardWords.Value, getExtendedWords));
        categories.Add("Nutcracker", ProcessCategoryWords(Config.NutcrackerWords.Value, getExtendedWords));
        categories.Add("Coil Head", ProcessCategoryWords(Config.CoilHeadWords.Value, getExtendedWords));
        categories.Add("Jester", ProcessCategoryWords(Config.JesterWords.Value, getExtendedWords));
        categories.Add("Masked", ProcessCategoryWords(Config.MaskedWords.Value, getExtendedWords));
        categories.Add("Eyeless Dog", ProcessCategoryWords(Config.EyelessDogWords.Value, getExtendedWords));
        categories.Add("Forest Keeper", ProcessCategoryWords(Config.ForestKeeperWords.Value, getExtendedWords));
        categories.Add("Earth Leviathan (Worm)",
            ProcessCategoryWords(Config.EarthLeviathanWords.Value, getExtendedWords));
        categories.Add("Baboon Hawk", ProcessCategoryWords(Config.BaboonHawkWords.Value, getExtendedWords));
        return categories;
    }

    private static HashSet<string> ProcessCategoryWords (string categoryWords, bool includeExtendedWords) {
        HashSet<string> words = new();
        foreach (var word in categoryWords.Split(",")) {
            if (StringIsAllUpper(word) && !includeExtendedWords) continue;
            words.Add(word.ToLower());
        }

        return words;
    }

    public static bool StringHasWords (string @string, HashSet<string> words) {
        return words.Any(word => @string.Contains(word));
    }

    public static void DisplayHUDTip (string title, string body, bool warning) {
        if (HUDManager.Instance == null) {
            logger.LogInfo("Failed to display tip, no active HUDManager");
            return;
        }

        HUDManager.Instance.DisplayTip(title, body, warning);
    }

    private static bool StringIsAllUpper (string word) {
        return word.All(c => !char.IsLetter(c) || char.IsUpper(c));
    }
}