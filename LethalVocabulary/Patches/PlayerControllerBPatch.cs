using GameNetcodeStuff;
using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerControllerBPatch {
    [HarmonyPostfix]
    [HarmonyPatch("ConnectClientToPlayerObject")]
    private static void PlayerConnected (ref PlayerControllerB __instance) {
        Plugin.Console.LogInfo("Joined lobby! Attempting to get config...");
        if (__instance.IsHost) {
            Plugin.Console.LogInfo("Loading config from file...");
            PunishmentManager.Instance.LoadConfig();
        }
        else {
            Plugin.Console.LogInfo("Requesting config from host...");
            PunishmentManager.Instance.RequestSettingsServerRpc(__instance.playerClientId);
        }

        PunishmentManager.Instance.LoadGameResources();
    }
}