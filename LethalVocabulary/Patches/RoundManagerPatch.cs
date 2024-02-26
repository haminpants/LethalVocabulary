using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(RoundManager))]
public class RoundManagerPatch {
    [HarmonyPostfix]
    [HarmonyPatch("FinishGeneratingNewLevelClientRpc")]
    private static void StartRound (ref RoundManager __instance) {
        if (!__instance.IsHost || PenaltyManager.Instance.roundInProgress) return;
        PenaltyManager.Instance.SetRoundInProgressServerRpc(true);
    }
}