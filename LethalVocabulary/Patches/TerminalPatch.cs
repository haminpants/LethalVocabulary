using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch {
    [HarmonyPrefix]
    [HarmonyPatch("ParsePlayerSentence")]
    private static void CheckSubmittedCommand (ref Terminal __instance) {
        var command = __instance.inputFieldText.text.ToLower();
        if (!Plugin.Instance.roundInProgress ||
            !Plugin.StringHasWords(command, Plugin.Instance.ClientBannedWords)) return;
        PenaltyManager.Instance.PunishPlayerServerRpc(__instance.NetworkManager.LocalClientId);
        if (command.StartsWith("transmit")) __instance.inputFieldText.text = "";
    }
}