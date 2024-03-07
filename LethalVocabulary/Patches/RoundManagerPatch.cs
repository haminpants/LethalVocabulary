using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(RoundManager))]
public class RoundManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("GenerateNewLevelClientRpc")]
    private static void SetMoonInProgress (ref RoundManager __instance) {
        if (!__instance.IsHost || PunishmentManager.Instance.MoonInProgress.Value) return;
        PunishmentManager.Instance.SetMoonInProgressServerRpc(true);
    }
}