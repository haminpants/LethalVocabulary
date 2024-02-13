using HarmonyLib;
using Unity.Netcode;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(GameNetworkManager))]
public class GameNetworkManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("Start")]
    private static void AddPenaltyManager (ref GameNetworkManager __instance) {
        __instance.GetComponent<NetworkManager>().AddNetworkPrefab(Plugin.Instance.penaltyManagerPrefab);
    }

    [HarmonyPostfix]
    [HarmonyPatch("StartDisconnect")]
    private static void PerformDisconnectOperations (ref GameNetworkManager __instance) {
        Plugin.Instance.EndRound();
    }
}