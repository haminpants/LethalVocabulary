using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace LethalVocabulary;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    public static Plugin Instance;
    public static GameObject NetcodeHelperPrefab;
    public static ManualLogSource Console;
    private Harmony _harmony;
    public SpeechRecognizer SpeechRecognizer;
    public new static Config Config { get; internal set; }

    private void Awake () {
        // Patch Netcode
        Type[] types = Assembly.GetExecutingAssembly().GetTypes();
        foreach (Type type in types) {
            MethodInfo[] methods =
                type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
            foreach (MethodInfo method in methods) {
                object[] attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                if (attributes.Length > 0) method.Invoke(null, null);
            }
        }

        // Load AssetBundle
        AssetBundle assetBundle = AssetBundle.LoadFromFile(
            Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "netcodehelper"));
        NetcodeHelperPrefab = assetBundle.LoadAsset<GameObject>("Assets/NetcodeHelper/NetcodeHelper.prefab");
        NetcodeHelperPrefab.AddComponent<PunishmentManager>();

        // Initialize Variables
        Instance = this;
        Console = Logger;
        SpeechRecognizer = new SpeechRecognizer();
        _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        Config = new Config(base.Config);

        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded! " +
                       $"Categories will be loaded by the host upon hosting a game.");
    }

    #region Helper Functions

    public static void DisplayHUDTip (string header, string body, bool isWarning = false) {
        if (HUDManager.Instance == null) {
            Console.LogWarning($"Failed to display HUD tip: {header} - {body}");
            return;
        }

        HUDManager.Instance.DisplayTip(header, body, isWarning);
    }

    public static void TeleportPlayer (ulong clientId, Vector3 teleportPosition) {
        PlayerControllerB player = StartOfRound.Instance.allPlayerScripts[clientId];
        AudioReverbPresets presets = FindObjectOfType<AudioReverbPresets>();
        if (presets) presets.audioPresets[2].ChangeAudioReverbForPlayer(player);
        player.DropAllHeldItems();
        player.isInElevator = false;
        player.isInHangarShipRoom = false;
        player.isInsideFactory = true;
        player.averageVelocity = 0;
        player.velocityLastFrame = Vector3.zero;
        player.TeleportPlayer(teleportPosition);
        player.beamOutParticle.Play();
        if (StartOfRound.Instance.localPlayerController.playerClientId == clientId)
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
    }

    #endregion
}