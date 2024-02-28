using HarmonyLib;

namespace LethalVocabulary.Patches;

[HarmonyPatch(typeof(Terminal))]
public class TerminalPatch {
    [HarmonyPrefix]
    [HarmonyPatch("ParsePlayerSentence")]
    private static void CheckSubmittedCommand (ref Terminal __instance) {
        string command = __instance.inputFieldText.text.ToLower();
        if (!PenaltyManager.Instance.StringIsLegal(command))
            PenaltyManager.Instance.PunishPlayerServerRpc(StartOfRound.Instance.localPlayerController.playerClientId);
        if (command.StartsWith("transmit")) __instance.inputFieldText.text = "";
    }
}