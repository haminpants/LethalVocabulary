using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(RoundManager))]
public class RoundManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("FinishGeneratingNewLevelClientRpc")]
    private static void PickNewBannedCategories (ref RoundManager __instance) {
        Plugin.Instance.StartRound();
    }
}