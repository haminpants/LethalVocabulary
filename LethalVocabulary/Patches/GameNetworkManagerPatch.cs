using HarmonyLib;
using Unity.Netcode;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(GameNetworkManager))]
public class GameNetworkManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void AddNetcodeHelperPrefab (ref GameNetworkManager __instance) {
        __instance.GetComponent<NetworkManager>().AddNetworkPrefab(Plugin.NetcodeHelperPrefab);
    }

    [HarmonyPostfix]
    [HarmonyPatch("StartDisconnect")]
    private static void PerformDisconnectOperations (ref GameNetworkManager __instance) {
        Plugin.Instance.SpeechRecognizer.StopRecognizer();
    }
}