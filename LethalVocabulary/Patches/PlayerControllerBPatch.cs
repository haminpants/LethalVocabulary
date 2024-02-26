using GameNetcodeStuff;
using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerControllerBPatch {
    [HarmonyPostfix]
    [HarmonyPatch("ConnectClientToPlayerObject")]
    private static void RequestHostSettings (ref PlayerControllerB __instance) {
        // TODO: later maybe load hosts categories and words so all players can have the same word list
        if (__instance.IsHost) return;
        Plugin.logger.LogInfo("Requesting settings from host...");
        PenaltyManager.Instance.RequestCurrentSettingsServerRpc(__instance.playerClientId);
    }
}