using GameNetcodeStuff;
using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(PlayerControllerB))]
public class PlayerControllerBPatch {
    [HarmonyPostfix]
    [HarmonyPatch("ConnectClientToPlayerObject")]
    private static void PlayerConnected (ref PlayerControllerB __instance) {
        if (__instance.IsHost) PunishmentManager.Instance.LoadConfig();
        else PunishmentManager.Instance.RequestHostCategoriesServerRpc(__instance.playerClientId);
        PunishmentManager.Instance.LoadGameResources();
    }
}