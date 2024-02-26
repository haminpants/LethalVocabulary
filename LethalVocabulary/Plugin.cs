using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace LethalVocabulary;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("me.loaforc.voicerecognitionapi")]
public class Plugin : BaseUnityPlugin {
    public static Plugin Instance;
    public static ManualLogSource logger;
    private static Harmony _harmony;
    public GameObject penaltyManagerPrefab;
    public SpeechRecognizer SpeechRecognizer;

    public new static Config Config { get; internal set; }

    private void Awake () {
        // Patch netcode
        var types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (var type in types) {
            var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (var method in methods) {
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0) method.Invoke(null, null);
            }
        }

        // Initialize variables
        Instance = this;
        logger = Logger;
        _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        SpeechRecognizer = new SpeechRecognizer();
        Config = new Config(base.Config);

        // Load assets
        string assetDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "lethalvocabularybundle");
        var bundle = AssetBundle.LoadFromFile(assetDir);
        penaltyManagerPrefab = bundle.LoadAsset<GameObject>("Assets/LethalVocabulary/PenaltyManager.prefab");
        penaltyManagerPrefab.AddComponent<PenaltyManager>();

        // Add event listener to the speech recognizer
        SpeechRecognizer.AddSpeechRecognizedHandler((_, e) => {
            string speech = e.Result.Text;
            float confidence = e.Result.Confidence;
            if (Config.LogRecognitions.Value) Logger.LogInfo($"Heard \"{speech}\" with {confidence * 100}% confidence");

            var player = StartOfRound.Instance.localPlayerController;
            if (confidence < 0.85 || player.isPlayerDead || PenaltyManager.Instance.StringIsLegal(speech)) return;
            PenaltyManager.Instance.PunishPlayerServerRpc(player.playerClientId);
        });

        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
    }

    // ================
    // Helper functions
    // ================
    public static void DisplayHUDTip (string title, string body, bool warning) {
        if (HUDManager.Instance == null) {
            logger.LogInfo("Failed to display tip, no active HUDManager");
            return;
        }

        HUDManager.Instance.DisplayTip(title, body, warning);
        logger.LogInfo($"Displayed Tip: {title} - {body}");
    }

    public static HashSet<string> ReadSeparatedString (string text, bool toLower = true, char separator = ',') {
        HashSet<string> words = new();
        foreach (string word in text.Split(separator)) words.Add(toLower ? word.ToLower() : word);

        return words;
    }

    public static bool StringHasWords (string text, HashSet<string> words) {
        return words.Any(text.Contains);
    }
}